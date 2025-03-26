import torch
import torch.nn as nn
import torch.nn.functional as F
import torch.optim as optim
import numpy as np
from typing import Dict, List, Tuple, Union, Optional
import os
import sys
import time
import asyncio
from collections import deque

# Add parent directory to path to allow imports
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
from models.a2c_model import A2CModel

class A2CAgent:
    """
    Advantage Actor-Critic agent for card battle game.
    """
    def __init__(self, 
                 state_dim: int = 140,  # Updated to match actual state tensor dimension
                 action_dim: int = 16,
                 hidden_dim: int = 256,
                 card_embedding_dim: int = 64,
                 lr: float = 0.001,
                 gamma: float = 0.99,
                 entropy_coef: float = 0.01,
                 value_loss_coef: float = 0.5,
                 max_grad_norm: float = 0.5,
                 device: str = 'cuda' if torch.cuda.is_available() else 'cpu'):
        """
        Initialize the A2C agent.
        
        Args:
            state_dim: Dimensionality of state representation
            action_dim: Dimensionality of action space
            hidden_dim: Dimensionality of hidden layers
            card_embedding_dim: Dimensionality of card embeddings
            lr: Learning rate
            gamma: Discount factor
            entropy_coef: Entropy coefficient for encouraging exploration
            value_loss_coef: Value loss coefficient
            max_grad_norm: Maximum gradient norm for clipping
            device: Device to use for tensor operations
        """
        self.gamma = gamma
        self.entropy_coef = entropy_coef
        self.value_loss_coef = value_loss_coef
        self.max_grad_norm = max_grad_norm
        self.device = torch.device(device)
        
        # Initialize model
        self.model = A2CModel(
            state_dim=state_dim,
            action_dim=action_dim,
            hidden_dim=hidden_dim,
            card_embedding_dim=card_embedding_dim
        ).to(self.device)
        
        # Initialize optimizer
        self.optimizer = optim.Adam(self.model.parameters(), lr=lr)
        
        # Initialize experience buffer
        self.states = []
        self.actions = []
        self.rewards = []
        self.dones = []
        self.action_masks = []
        self.values = []
        self.log_probs = []
        
        # Statistics
        self.episode_rewards = []
        self.wins = 0
        self.losses = 0
        self.training_step = 0
        
        # Experience tracking for parallel agents
        self.pending_experiences = {}
    
    def select_action(self, state: Dict, valid_actions: Dict) -> Tuple[int, Dict]:
        """
        Select an action based on the current state and valid actions.
        
        Args:
            state: Current state observation
            valid_actions: Dictionary containing valid actions
            
        Returns:
            Tuple[int, Dict]: Selected action index and the action details
        """
        with torch.no_grad():
            # Create action mask based on valid actions
            action_mask = self._create_action_mask(valid_actions)
            
            # Forward pass of the model
            try:
                policy_logits, value = self.model(state, action_mask)
            except RuntimeError as e:
                if "dimensions" in str(e):
                    # Handle dimension mismatch errors more gracefully
                    print(f"Dimension error in model forward pass: {e}")
                    # Reset the model's hidden state which might help
                    self.model.reset_hidden()
                    # Try again with a clean state
                    policy_logits, value = self.model(state, action_mask)
                else:
                    # Re-raise other errors
                    raise
            
            # Apply softmax to get probabilities
            probs = F.softmax(policy_logits, dim=1)
            
            # Sample action from the distribution
            action_idx = torch.multinomial(probs, 1).item()
            
            # Get log probability of the selected action
            log_prob = F.log_softmax(policy_logits, dim=1)[0, action_idx]
            
            # Convert action index to actual game action
            action = self._action_idx_to_game_action(action_idx, valid_actions)
            
            # Store experience
            self.states.append(state)
            self.actions.append(action_idx)
            self.values.append(value)
            self.log_probs.append(log_prob)
            self.action_masks.append(action_mask)
            
            return action_idx, action
    
    def _create_action_mask(self, valid_actions: Dict) -> torch.Tensor:
        """
        Create a binary mask of valid actions.
        
        Args:
            valid_actions: Dictionary containing valid actions
            
        Returns:
            torch.Tensor: Binary mask where 1 indicates a valid action
        """
        # Assumes fixed action space dimensionality
        action_dim = self.model.actor[-1].out_features
        mask = torch.zeros(1, action_dim, device=self.device)
        
        if valid_actions['type'] == 'map':
            # Map actions
            # Actions 0-9 reserved for challenging specific players
            available_players = valid_actions.get('available_players', [])
            for i, player_id in enumerate(available_players[:10]):  # Limit to first 10 players
                mask[0, i] = 1
                
        elif valid_actions['type'] == 'battle':
            # Battle actions
            if valid_actions.get('can_end_turn', False):
                # End turn action (assumed to be the last action)
                mask[0, action_dim-1] = 1
                
            # Card play actions (assumed to be action indices 0 to n-1 where n is max cards)
            playable_cards = valid_actions.get('playable_cards', [])
            for i, card in enumerate(playable_cards[:action_dim-1]):  # Reserve last action for end turn
                mask[0, i] = 1
        
        return mask
    
    def _action_idx_to_game_action(self, action_idx: int, valid_actions: Dict) -> Dict:
        """
        Convert an action index to an actual game action.
        
        Args:
            action_idx: Index of the selected action
            valid_actions: Dictionary containing valid actions
            
        Returns:
            Dict: Game action details
        """
        action_dim = self.model.actor[-1].out_features
        
        if valid_actions['type'] == 'map':
            # Map actions
            available_players = valid_actions.get('available_players', [])
            if action_idx < len(available_players):
                # Challenge player action
                return {
                    'type': 'challenge',
                    'target_id': available_players[action_idx]
                }
            else:
                # Wait action
                return {
                    'type': 'wait'
                }
                
        elif valid_actions['type'] == 'battle':
            # Battle actions
            if action_idx == action_dim - 1:
                # End turn action
                return {
                    'type': 'end_turn'
                }
            else:
                # Play card action
                playable_cards = valid_actions.get('playable_cards', [])
                if action_idx < len(playable_cards):
                    return {
                        'type': 'play_card',
                        'card_id': playable_cards[action_idx].get('Id')
                    }
                else:
                    # Default to end turn if selected card index is invalid
                    return {
                        'type': 'end_turn'
                    }
        
        # Default wait action
        return {
            'type': 'wait'
        }
    
    def add_reward(self, reward: float, done: bool = False):
        """
        Add a reward to the current episode.
        
        Args:
            reward: Reward value
            done: Whether this is a terminal state
        """
        self.rewards.append(reward)
        self.dones.append(done)
        
        if done:
            # Store total episode reward
            episode_reward = sum(self.rewards)
            self.episode_rewards.append(episode_reward)
            
            # Update win/loss counters
            if episode_reward > 0:
                self.wins += 1
            else:
                self.losses += 1
    
    def train(self, next_value: float = 0.0) -> Dict:
        """
        Train the agent on collected experiences.
        
        Args:
            next_value: Value estimate for the next state
            
        Returns:
            Dict: Training statistics
        """
        # Check if there are enough experiences to train
        if len(self.rewards) == 0:
            return {
                'policy_loss': 0.0,
                'value_loss': 0.0,
                'entropy': 0.0,
                'total_loss': 0.0
            }
        
        # Calculate returns and advantages
        returns = self._compute_returns(next_value)
        advantages = self._compute_advantages(returns)
        
        # Convert lists to tensors
        returns_tensor = torch.cat(returns).detach()
        advantages_tensor = torch.cat(advantages).detach()
        values_tensor = torch.cat(self.values).detach()
        log_probs_tensor = torch.tensor(self.log_probs, device=self.device).detach()
        actions_tensor = torch.tensor(self.actions, device=self.device).detach().unsqueeze(1)
        
        # Compute policy loss
        policy_loss = -(advantages_tensor * log_probs_tensor).mean()
        
        # Compute value loss
        value_loss = F.mse_loss(values_tensor, returns_tensor)
        
        # Compute entropy (for exploration)
        entropy = 0.0  # Placeholder, will calculate actual entropy during training
        
        # Compute total loss
        total_loss = policy_loss + self.value_loss_coef * value_loss - self.entropy_coef * entropy
        
        # Optimize
        self.optimizer.zero_grad()
        total_loss.backward()
        
        # Clip gradients
        torch.nn.utils.clip_grad_norm_(self.model.parameters(), self.max_grad_norm)
        
        # Step optimizer
        self.optimizer.step()
        
        # Increment training step
        self.training_step += 1
        
        # Clear experience buffer
        self._clear_experiences()
        
        # Reset LSTM hidden state
        self.model.reset_hidden()
        
        return {
            'policy_loss': policy_loss.item(),
            'value_loss': value_loss.item(),
            'entropy': entropy,
            'total_loss': total_loss.item()
        }
    
    def _compute_returns(self, next_value: float) -> List[torch.Tensor]:
        """
        Compute returns using discounted rewards.
        
        Args:
            next_value: Value estimate for the next state
            
        Returns:
            List[torch.Tensor]: List of return tensors
        """
        returns = []
        R = torch.tensor([[next_value]], device=self.device)
        
        # Compute returns in reverse order
        for step in reversed(range(len(self.rewards))):
            # R = reward + gamma * R * (1 - done)
            R = torch.tensor([[self.rewards[step]]], device=self.device) + \
                 self.gamma * R * (1 - torch.tensor([[float(self.dones[step])]], device=self.device))
            returns.insert(0, R)
        
        return returns
    
    def _compute_advantages(self, returns: List[torch.Tensor]) -> List[torch.Tensor]:
        """
        Compute advantages as the difference between returns and value estimates.
        
        Args:
            returns: List of return tensors
            
        Returns:
            List[torch.Tensor]: List of advantage tensors
        """
        advantages = []
        for step in range(len(self.values)):
            advantages.append(returns[step] - self.values[step])
        
        return advantages
    
    def _clear_experiences(self):
        """Clear all experience buffers."""
        self.states.clear()
        self.actions.clear()
        self.rewards.clear()
        self.dones.clear()
        self.values.clear()
        self.log_probs.clear()
        self.action_masks.clear()
    
    def save_model(self, path: str):
        """
        Save the model to a file.
        
        Args:
            path: Path to save the model to
        """
        # Create directory if it doesn't exist
        os.makedirs(os.path.dirname(path), exist_ok=True)
        
        # Save model state
        torch.save({
            'model_state_dict': self.model.state_dict(),
            'optimizer_state_dict': self.optimizer.state_dict(),
            'training_step': self.training_step,
            'wins': self.wins,
            'losses': self.losses,
            'episode_rewards': self.episode_rewards
        }, path)
    
    def load_model(self, path: str):
        """
        Load the model from a file.
        
        Args:
            path: Path to load the model from
        """
        # Check if file exists
        if not os.path.exists(path):
            print(f"No model found at {path}")
            return
        
        # Load model state
        checkpoint = torch.load(path, map_location=self.device)
        self.model.load_state_dict(checkpoint['model_state_dict'])
        self.optimizer.load_state_dict(checkpoint['optimizer_state_dict'])
        self.training_step = checkpoint.get('training_step', 0)
        self.wins = checkpoint.get('wins', 0)
        self.losses = checkpoint.get('losses', 0)
        self.episode_rewards = checkpoint.get('episode_rewards', [])
        
        # Reset LSTM hidden state
        self.model.reset_hidden()
    
    def get_stats(self) -> Dict:
        """
        Get agent statistics.
        
        Returns:
            Dict: Statistics dictionary
        """
        win_rate = self.wins / max(1, self.wins + self.losses)
        recent_rewards = self.episode_rewards[-100:] if self.episode_rewards else []
        avg_reward = sum(recent_rewards) / max(1, len(recent_rewards))
        
        return {
            'training_step': self.training_step,
            'win_rate': win_rate,
            'wins': self.wins,
            'losses': self.losses,
            'avg_reward': avg_reward,
            'num_episodes': len(self.episode_rewards)
        }
    
    def add_pending_experience(self, agent_id: str, experience: Dict):
        """
        Add a pending experience from a parallel agent.
        
        Args:
            agent_id: ID of the agent providing the experience
            experience: Experience dictionary
        """
        if agent_id not in self.pending_experiences:
            self.pending_experiences[agent_id] = []
        
        self.pending_experiences[agent_id].append(experience)
    
    def process_pending_experiences(self):
        """Process all pending experiences from parallel agents."""
        for agent_id, experiences in self.pending_experiences.items():
            for exp in experiences:
                self.states.append(exp['state'])
                self.actions.append(exp['action'])
                self.rewards.append(exp['reward'])
                self.dones.append(exp['done'])
                self.values.append(exp['value'])
                self.log_probs.append(exp['log_prob'])
                self.action_masks.append(exp['action_mask'])
                
                if exp['done']:
                    episode_reward = sum([e['reward'] for e in experiences if 'reward' in e])
                    self.episode_rewards.append(episode_reward)
                    
                    if episode_reward > 0:
                        self.wins += 1
                    else:
                        self.losses += 1
        
        # Clear pending experiences
        self.pending_experiences.clear()
