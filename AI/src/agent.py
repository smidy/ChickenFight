"""
Agent implementation for the card battle game.
"""
import os
import time
import logging
import threading
import numpy as np
from typing import Dict, List, Tuple, Optional, Any, Union

from .environment import CardBattleEnv
from .models.a2c import A2C, RolloutBuffer
from .utils.config import Config
from .utils.logger import MetricsLogger


class Agent:
    """
    Agent for the card battle game.
    
    This agent connects to the game server, navigates the map, challenges players to fights,
    and uses an A2C model to learn how to play the card battle game.
    """
    
    def __init__(self, config: Config, agent_id: str = "", logger: Optional[MetricsLogger] = None):
        """
        Initialize the agent.
        
        Args:
            config: Configuration
            agent_id: Unique identifier for this agent
            logger: Metrics logger
        """
        self.config = config
        self.agent_id = agent_id or f"agent_{np.random.randint(0, 10000)}"
        self.logger = logger or MetricsLogger(log_dir=os.path.join("logs", self.agent_id))
        
        # Set up logging
        self.log = logging.getLogger(f"Agent-{self.agent_id}")
        self.log.setLevel(logging.INFO)
        
        # Create environment
        self.env = CardBattleEnv(config, agent_id=self.agent_id)
        
        # Create A2C model
        self.model = A2C(
            config=config,
            action_dim=self.env.action_space.n,
            logger=self.logger
        )
        
        # Create rollout buffer
        self.buffer = RolloutBuffer(capacity=config.training.replay_buffer_size)
        
        # Training state
        self.is_training = False
        self.training_thread: Optional[threading.Thread] = None
        self.stop_training = False
        
        # Episode metrics
        self.episode_rewards = []
        self.episode_lengths = []
        self.episode_wins = []
        self.current_episode_reward = 0.0
        self.current_episode_length = 0
    
    def connect(self) -> bool:
        """
        Connect to the game server.
        
        Returns:
            True if connection was successful, False otherwise
        """
        return self.env.connect()
    
    def disconnect(self) -> None:
        """Disconnect from the game server."""
        self.env.disconnect()
    
    def join_map(self, map_id: Optional[str] = None) -> bool:
        """
        Join a map.
        
        Args:
            map_id: ID of the map to join, or None to join the first available map
            
        Returns:
            True if join was successful, False otherwise
        """
        return self.env.join_map(map_id)
    
    def find_and_challenge_player(self) -> bool:
        """
        Find a player who is not in a fight and challenge them.
        
        Returns:
            True if challenge was successful, False otherwise
        """
        # Find a player
        target_id = self.env.find_player_not_in_fight()
        if target_id is None:
            self.log.warning("No players to challenge, waiting...")
            # Wait for a player to become available
            time.sleep(5.0)
            return False
        
        # Challenge the player
        return self.env.challenge_player(target_id)
    
    def train_episode(self) -> Dict[str, float]:
        """
        Train for a single episode.
        
        Returns:
            Dictionary of episode metrics
        """
        # Reset environment
        state, info = self.env.reset()
        
        # Reset episode metrics
        self.current_episode_reward = 0.0
        self.current_episode_length = 0
        done = False
        
        # Episode loop
        while not done:
            # Select action
            action = self.model.select_action(state)
            
            # Take action
            next_state, reward, terminated, truncated, info = self.env.step(action)
            done = terminated or truncated
            
            # Store transition
            self.buffer.add(state, action, reward, next_state, done)
            
            # Update state
            state = next_state
            
            # Update episode metrics
            self.current_episode_reward += reward
            self.current_episode_length += 1
            
            # Update model if buffer has enough samples
            if self.buffer.size >= self.config.training.batch_size:
                # Sample from buffer
                states, actions, rewards, next_states, dones = self.buffer.sample(
                    self.config.training.batch_size
                )
                
                # Update model
                metrics = self.model.update(states, actions, rewards, next_states, dones)
                
                # Log metrics
                if self.logger:
                    self.logger.log_metrics(
                        self.model.episodes_completed,
                        metrics
                    )
        
        # Episode completed
        self.model.increment_episodes()
        
        # Store episode metrics
        self.episode_rewards.append(self.current_episode_reward)
        self.episode_lengths.append(self.current_episode_length)
        self.episode_wins.append(info.get("won", False))
        
        # Calculate metrics
        metrics = {
            "episode_reward": self.current_episode_reward,
            "episode_length": self.current_episode_length,
            "episode_win": 1.0 if info.get("won", False) else 0.0,
            "win_rate": np.mean(self.episode_wins[-100:]) if self.episode_wins else 0.0,
            "mean_reward": np.mean(self.episode_rewards[-100:]) if self.episode_rewards else 0.0,
            "mean_length": np.mean(self.episode_lengths[-100:]) if self.episode_lengths else 0.0
        }
        
        # Log metrics
        if self.logger:
            self.logger.log_metrics(
                self.model.episodes_completed,
                metrics
            )
        
        # Save checkpoint if needed
        if (self.model.episodes_completed % self.config.training.save_interval == 0 and
            self.model.episodes_completed > 0):
            self.save_checkpoint()
        
        # Log episode
        self.log.info(
            f"Episode {self.model.episodes_completed} completed: "
            f"reward={self.current_episode_reward:.2f}, "
            f"length={self.current_episode_length}, "
            f"win={info.get('won', False)}, "
            f"win_rate={metrics['win_rate']:.2f}"
        )
        
        return metrics
    
    def train(self, num_episodes: Optional[int] = None) -> None:
        """
        Train the agent for a specified number of episodes.
        
        Args:
            num_episodes: Number of episodes to train for, or None for unlimited
        """
        if self.is_training:
            self.log.warning("Agent is already training")
            return
        
        self.is_training = True
        self.stop_training = False
        
        # Connect to server if not connected
        if not self.env.connected:
            if not self.connect():
                self.log.error("Failed to connect to server")
                self.is_training = False
                return
        
        # Join a map if not on one
        if not self.env.current_map_id:
            if not self.join_map():
                self.log.error("Failed to join a map")
                self.is_training = False
                return
        
        # Start training thread
        self.training_thread = threading.Thread(
            target=self._train_loop,
            args=(num_episodes,)
        )
        self.training_thread.daemon = True
        self.training_thread.start()
    
    def _train_loop(self, num_episodes: Optional[int] = None) -> None:
        """
        Training loop.
        
        Args:
            num_episodes: Number of episodes to train for, or None for unlimited
        """
        try:
            episode_count = 0
            while not self.stop_training:
                # Check if we've reached the episode limit
                if num_episodes is not None and episode_count >= num_episodes:
                    break
                
                # Train for one episode
                self.train_episode()
                episode_count += 1
        except Exception as e:
            self.log.error(f"Error in training loop: {e}")
        finally:
            self.is_training = False
    
    def stop(self) -> None:
        """Stop training."""
        if not self.is_training:
            self.log.warning("Agent is not training")
            return
        
        self.stop_training = True
        if self.training_thread is not None:
            self.training_thread.join(timeout=5.0)
            if self.training_thread.is_alive():
                self.log.warning("Training thread did not terminate properly")
        
        self.is_training = False
    
    def save_checkpoint(self, path: Optional[str] = None) -> str:
        """
        Save model checkpoint.
        
        Args:
            path: Path to save checkpoint, or None to use default path
            
        Returns:
            Path where checkpoint was saved
        """
        checkpoint_path = self.model.save_checkpoint(path)
        self.log.info(f"Saved checkpoint to {checkpoint_path}")
        return checkpoint_path
    
    def load_checkpoint(self, path: str) -> None:
        """
        Load model checkpoint.
        
        Args:
            path: Path to load checkpoint from
        """
        self.model.load_checkpoint(path)
        self.log.info(f"Loaded checkpoint from {path}")
    
    def evaluate(self, num_episodes: int = 10) -> Dict[str, float]:
        """
        Evaluate the agent for a specified number of episodes.
        
        Args:
            num_episodes: Number of episodes to evaluate for
            
        Returns:
            Dictionary of evaluation metrics
        """
        # Connect to server if not connected
        if not self.env.connected:
            if not self.connect():
                self.log.error("Failed to connect to server")
                return {}
        
        # Join a map if not on one
        if not self.env.current_map_id:
            if not self.join_map():
                self.log.error("Failed to join a map")
                return {}
        
        # Evaluation metrics
        rewards = []
        lengths = []
        wins = []
        
        # Evaluate for num_episodes
        for i in range(num_episodes):
            # Reset environment
            state, info = self.env.reset()
            
            # Reset episode metrics
            episode_reward = 0.0
            episode_length = 0
            done = False
            
            # Episode loop
            while not done:
                # Select action (deterministic)
                action = self.model.select_action(state, deterministic=True)
                
                # Take action
                next_state, reward, terminated, truncated, info = self.env.step(action)
                done = terminated or truncated
                
                # Update state
                state = next_state
                
                # Update episode metrics
                episode_reward += reward
                episode_length += 1
            
            # Store episode metrics
            rewards.append(episode_reward)
            lengths.append(episode_length)
            wins.append(info.get("won", False))
            
            # Log episode
            self.log.info(
                f"Evaluation episode {i+1}/{num_episodes}: "
                f"reward={episode_reward:.2f}, "
                f"length={episode_length}, "
                f"win={info.get('won', False)}"
            )
        
        # Calculate metrics
        metrics = {
            "mean_reward": np.mean(rewards),
            "mean_length": np.mean(lengths),
            "win_rate": np.mean(wins)
        }
        
        # Log metrics
        self.log.info(
            f"Evaluation completed: "
            f"mean_reward={metrics['mean_reward']:.2f}, "
            f"mean_length={metrics['mean_length']:.2f}, "
            f"win_rate={metrics['win_rate']:.2f}"
        )
        
        return metrics
