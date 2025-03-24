"""
Training orchestration for the card battle game.
"""
import os
import time
import logging
import threading
import numpy as np
from typing import Dict, List, Tuple, Optional, Any, Union

from ..agent import Agent
from ..utils.config import Config
from ..utils.logger import MetricsLogger


class Trainer:
    """
    Trainer for the card battle game.
    
    This class manages multiple agents training in parallel.
    """
    
    def __init__(self, config: Config, logger: Optional[MetricsLogger] = None):
        """
        Initialize the trainer.
        
        Args:
            config: Configuration
            logger: Metrics logger
        """
        self.config = config
        self.logger = logger or MetricsLogger(log_dir="logs/trainer")
        
        # Set up logging
        self.log = logging.getLogger("Trainer")
        self.log.setLevel(logging.INFO)
        
        # Create agents
        self.agents: List[Agent] = []
        for i in range(config.training.num_agents):
            agent_id = f"agent_{i}"
            agent_logger = MetricsLogger(log_dir=os.path.join("logs", agent_id))
            agent = Agent(config, agent_id=agent_id, logger=agent_logger)
            self.agents.append(agent)
        
        # Training state
        self.is_training = False
        self.training_thread: Optional[threading.Thread] = None
        self.stop_training = False
        
        # Create checkpoint directory
        os.makedirs(config.training.checkpoint_dir, exist_ok=True)
    
    def start_training(self, num_episodes: Optional[int] = None) -> None:
        """
        Start training all agents.
        
        Args:
            num_episodes: Number of episodes to train for, or None for unlimited
        """
        if self.is_training:
            self.log.warning("Training is already in progress")
            return
        
        self.is_training = True
        self.stop_training = False
        
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
            # Start training for each agent
            for agent in self.agents:
                agent.train(num_episodes)
            
            # Wait for all agents to complete training
            while not self.stop_training:
                # Check if all agents have completed training
                if all(not agent.is_training for agent in self.agents):
                    break
                
                # Log training progress
                self._log_training_progress()
                
                # Sleep
                time.sleep(10.0)
        except Exception as e:
            self.log.error(f"Error in training loop: {e}")
        finally:
            # Stop all agents
            for agent in self.agents:
                if agent.is_training:
                    agent.stop()
            
            self.is_training = False
    
    def _log_training_progress(self) -> None:
        """Log training progress."""
        # Collect metrics from all agents
        total_episodes = sum(agent.model.episodes_completed for agent in self.agents)
        win_rates = [
            np.mean(agent.episode_wins[-100:]) if agent.episode_wins else 0.0
            for agent in self.agents
        ]
        mean_rewards = [
            np.mean(agent.episode_rewards[-100:]) if agent.episode_rewards else 0.0
            for agent in self.agents
        ]
        
        # Log metrics
        self.log.info(
            f"Training progress: "
            f"total_episodes={total_episodes}, "
            f"mean_win_rate={np.mean(win_rates):.2f}, "
            f"mean_reward={np.mean(mean_rewards):.2f}"
        )
        
        # Log to metrics logger
        if self.logger:
            self.logger.log_metrics(
                total_episodes,
                {
                    "mean_win_rate": np.mean(win_rates),
                    "mean_reward": np.mean(mean_rewards)
                }
            )
    
    def stop_training(self) -> None:
        """Stop training all agents."""
        if not self.is_training:
            self.log.warning("Training is not in progress")
            return
        
        self.stop_training = True
        
        # Stop all agents
        for agent in self.agents:
            if agent.is_training:
                agent.stop()
        
        # Wait for training thread to complete
        if self.training_thread is not None:
            self.training_thread.join(timeout=5.0)
            if self.training_thread.is_alive():
                self.log.warning("Training thread did not terminate properly")
        
        self.is_training = False
    
    def save_checkpoints(self, path: Optional[str] = None) -> List[str]:
        """
        Save checkpoints for all agents.
        
        Args:
            path: Base path to save checkpoints, or None to use default path
            
        Returns:
            List of paths where checkpoints were saved
        """
        checkpoint_paths = []
        for i, agent in enumerate(self.agents):
            if path is not None:
                agent_path = f"{path}_agent_{i}.pt"
            else:
                agent_path = None
            
            checkpoint_path = agent.save_checkpoint(agent_path)
            checkpoint_paths.append(checkpoint_path)
        
        return checkpoint_paths
    
    def load_checkpoints(self, paths: List[str]) -> None:
        """
        Load checkpoints for all agents.
        
        Args:
            paths: List of paths to load checkpoints from
        """
        if len(paths) != len(self.agents):
            self.log.error(f"Number of paths ({len(paths)}) does not match number of agents ({len(self.agents)})")
            return
        
        for agent, path in zip(self.agents, paths):
            agent.load_checkpoint(path)
    
    def evaluate(self, num_episodes: int = 10) -> Dict[str, float]:
        """
        Evaluate all agents.
        
        Args:
            num_episodes: Number of episodes to evaluate for
            
        Returns:
            Dictionary of evaluation metrics
        """
        # Evaluate each agent
        metrics_list = []
        for agent in self.agents:
            metrics = agent.evaluate(num_episodes)
            metrics_list.append(metrics)
        
        # Aggregate metrics
        aggregated_metrics = {}
        for key in metrics_list[0].keys():
            values = [metrics[key] for metrics in metrics_list]
            aggregated_metrics[key] = np.mean(values)
        
        # Log metrics
        self.log.info(
            f"Evaluation completed: "
            f"mean_reward={aggregated_metrics.get('mean_reward', 0.0):.2f}, "
            f"mean_length={aggregated_metrics.get('mean_length', 0.0):.2f}, "
            f"win_rate={aggregated_metrics.get('win_rate', 0.0):.2f}"
        )
        
        return aggregated_metrics
