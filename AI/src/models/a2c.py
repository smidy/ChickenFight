"""
Advantage Actor-Critic (A2C) model implementation.
"""
import os
import time
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from typing import Dict, List, Tuple, Optional, Any, Union

from .network import CardBattleNetwork
from ..utils.config import Config, ModelConfig, TrainingConfig
from ..utils.logger import MetricsLogger


class A2C:
    """
    Advantage Actor-Critic (A2C) model.
    
    This class implements the A2C algorithm for reinforcement learning.
    """
    
    def __init__(self, config: Config, action_dim: int, logger: Optional[MetricsLogger] = None):
        """
        Initialize the A2C model.
        
        Args:
            config: Configuration
            action_dim: Dimension of the action space
            logger: Metrics logger
        """
        self.config = config
        self.model_config = config.model
        self.training_config = config.training
        self.action_dim = action_dim
        self.logger = logger
        
        # Set device
        self.device = torch.device("cuda" if self.model_config.use_cuda and torch.cuda.is_available() else "cpu")
        if self.model_config.use_cuda and torch.cuda.is_available():
            torch.cuda.set_device(self.model_config.cuda_device)
        
        # Create network
        self.network = CardBattleNetwork(self.model_config, action_dim).to(self.device)
        
        # Create optimizer
        self.optimizer = optim.Adam(
            self.network.parameters(),
            lr=self.training_config.learning_rate
        )
        
        # Create checkpoint directory
        os.makedirs(self.training_config.checkpoint_dir, exist_ok=True)
        
        # Training metrics
        self.total_steps = 0
        self.episodes_completed = 0
        self.epsilon = self.training_config.initial_epsilon
    
    def select_action(self, state: Dict[str, np.ndarray], deterministic: bool = False) -> int:
        """
        Select an action based on the current state.
        
        Args:
            state: Current state
            deterministic: Whether to use deterministic action selection
            
        Returns:
            Selected action
        """
        # Convert state to tensors
        state_tensors = {}
        for key, value in state.items():
            if isinstance(value, np.ndarray):
                state_tensors[key] = torch.FloatTensor(value).unsqueeze(0).to(self.device)
        
        # Epsilon-greedy exploration
        if not deterministic and np.random.random() < self.epsilon:
            # Random action
            return np.random.randint(0, self.action_dim)
        
        # Get action from network
        with torch.no_grad():
            action, _, _ = self.network.get_action_and_value(state_tensors, deterministic=deterministic)
        
        return action.cpu().numpy()[0]
    
    def update(self, states: List[Dict[str, np.ndarray]], actions: List[int], 
              rewards: List[float], next_states: List[Dict[str, np.ndarray]], 
              dones: List[bool]) -> Dict[str, float]:
        """
        Update the model based on collected experiences.
        
        Args:
            states: List of states
            actions: List of actions
            rewards: List of rewards
            next_states: List of next states
            dones: List of done flags
            
        Returns:
            Dictionary of training metrics
        """
        # Convert to tensors
        batch_size = len(states)
        
        # Process states and next_states
        state_tensors = {}
        next_state_tensors = {}
        
        # Initialize tensors for each key in the state dictionary
        for key in states[0].keys():
            # Get the shape of the first state's value for this key
            shape = states[0][key].shape
            
            # Create tensors for states and next_states
            state_tensors[key] = torch.zeros((batch_size,) + shape, dtype=torch.float32).to(self.device)
            next_state_tensors[key] = torch.zeros((batch_size,) + shape, dtype=torch.float32).to(self.device)
            
            # Fill the tensors with data
            for i in range(batch_size):
                state_tensors[key][i] = torch.FloatTensor(states[i][key])
                next_state_tensors[key][i] = torch.FloatTensor(next_states[i][key])
        
        # Convert other data to tensors
        actions_tensor = torch.LongTensor(actions).to(self.device)
        rewards_tensor = torch.FloatTensor(rewards).to(self.device)
        dones_tensor = torch.FloatTensor(dones).to(self.device)
        
        # Compute returns and advantages
        with torch.no_grad():
            _, next_values = self.network(next_state_tensors)
            next_values = next_values.squeeze(-1)
            
            # Compute returns (bootstrapped)
            returns = rewards_tensor + self.training_config.gamma * next_values * (1 - dones_tensor)
        
        # Forward pass
        action_log_probs, values, entropy = self.network.evaluate_actions(state_tensors, actions_tensor)
        values = values.squeeze(-1)
        
        # Compute advantages
        advantages = returns - values
        
        # Compute losses
        value_loss = advantages.pow(2).mean()
        policy_loss = -(advantages.detach() * action_log_probs).mean()
        
        # Total loss
        loss = (
            policy_loss
            + self.training_config.value_loss_coef * value_loss
            - self.training_config.entropy_coef * entropy
        )
        
        # Optimize
        self.optimizer.zero_grad()
        loss.backward()
        
        # Clip gradients
        nn.utils.clip_grad_norm_(
            self.network.parameters(),
            self.training_config.max_grad_norm
        )
        
        self.optimizer.step()
        
        # Update epsilon for exploration
        self.epsilon = max(
            self.training_config.final_epsilon,
            self.epsilon - (self.training_config.initial_epsilon - self.training_config.final_epsilon) / 
            self.training_config.epsilon_decay_episodes
        )
        
        # Update step counter
        self.total_steps += batch_size
        
        # Return metrics
        metrics = {
            "loss": loss.item(),
            "policy_loss": policy_loss.item(),
            "value_loss": value_loss.item(),
            "entropy": entropy.item(),
            "epsilon": self.epsilon
        }
        
        return metrics
    
    def save_checkpoint(self, path: Optional[str] = None) -> str:
        """
        Save model checkpoint.
        
        Args:
            path: Path to save checkpoint, or None to use default path
            
        Returns:
            Path where checkpoint was saved
        """
        if path is None:
            path = os.path.join(
                self.training_config.checkpoint_dir,
                f"a2c_checkpoint_{self.episodes_completed}.pt"
            )
        
        torch.save({
            "network_state_dict": self.network.state_dict(),
            "optimizer_state_dict": self.optimizer.state_dict(),
            "total_steps": self.total_steps,
            "episodes_completed": self.episodes_completed,
            "epsilon": self.epsilon,
            "config": {
                "model": self.model_config.__dict__,
                "training": self.training_config.__dict__
            }
        }, path)
        
        return path
    
    def load_checkpoint(self, path: str) -> None:
        """
        Load model checkpoint.
        
        Args:
            path: Path to load checkpoint from
        """
        checkpoint = torch.load(path, map_location=self.device)
        
        self.network.load_state_dict(checkpoint["network_state_dict"])
        self.optimizer.load_state_dict(checkpoint["optimizer_state_dict"])
        self.total_steps = checkpoint["total_steps"]
        self.episodes_completed = checkpoint["episodes_completed"]
        self.epsilon = checkpoint["epsilon"]
        
        # Log
        if self.logger:
            self.logger.logger.info(f"Loaded checkpoint from {path}")
            self.logger.logger.info(f"Episodes completed: {self.episodes_completed}")
            self.logger.logger.info(f"Total steps: {self.total_steps}")
    
    def increment_episodes(self, count: int = 1) -> None:
        """
        Increment the episode counter.
        
        Args:
            count: Number of episodes to increment by
        """
        self.episodes_completed += count


class RolloutBuffer:
    """
    Buffer for storing rollout information.
    """
    
    def __init__(self, capacity: int):
        """
        Initialize the buffer.
        
        Args:
            capacity: Maximum capacity of the buffer
        """
        self.capacity = capacity
        self.states = []
        self.actions = []
        self.rewards = []
        self.next_states = []
        self.dones = []
        self.size = 0
    
    def add(self, state: Dict[str, np.ndarray], action: int, reward: float, 
           next_state: Dict[str, np.ndarray], done: bool) -> None:
        """
        Add a transition to the buffer.
        
        Args:
            state: Current state
            action: Action taken
            reward: Reward received
            next_state: Next state
            done: Whether the episode is done
        """
        if self.size < self.capacity:
            self.states.append(state)
            self.actions.append(action)
            self.rewards.append(reward)
            self.next_states.append(next_state)
            self.dones.append(done)
            self.size += 1
        else:
            # Replace oldest entry
            idx = self.size % self.capacity
            self.states[idx] = state
            self.actions[idx] = action
            self.rewards[idx] = reward
            self.next_states[idx] = next_state
            self.dones[idx] = done
    
    def sample(self, batch_size: int) -> Tuple[List[Dict[str, np.ndarray]], List[int], List[float], List[Dict[str, np.ndarray]], List[bool]]:
        """
        Sample a batch of transitions from the buffer.
        
        Args:
            batch_size: Number of transitions to sample
            
        Returns:
            Batch of transitions (states, actions, rewards, next_states, dones)
        """
        indices = np.random.choice(self.size, min(batch_size, self.size), replace=False)
        
        return (
            [self.states[i] for i in indices],
            [self.actions[i] for i in indices],
            [self.rewards[i] for i in indices],
            [self.next_states[i] for i in indices],
            [self.dones[i] for i in indices]
        )
    
    def clear(self) -> None:
        """Clear the buffer."""
        self.states = []
        self.actions = []
        self.rewards = []
        self.next_states = []
        self.dones = []
        self.size = 0
