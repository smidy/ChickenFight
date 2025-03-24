"""
Neural network architecture for the A2C model.
"""
import torch
import torch.nn as nn
import torch.nn.functional as F
from typing import Dict, Tuple, List, Optional, Union, Any

from ..utils.config import ModelConfig


class CardBattleNetwork(nn.Module):
    """
    Neural network for the card battle game.
    
    This network takes the state representation and outputs:
    1. Policy (action probabilities)
    2. Value (state value estimate)
    """
    
    def __init__(self, config: ModelConfig, action_dim: int):
        """
        Initialize the network.
        
        Args:
            config: Model configuration
            action_dim: Dimension of the action space
        """
        super().__init__()
        
        self.config = config
        self.action_dim = action_dim
        
        # Input dimensions
        self.state_dim = config.state_dim
        self.hidden_dim = config.hidden_dim
        
        # Feature extraction layers
        
        # Player state features
        self.player_state_fc = nn.Sequential(
            nn.Linear(4, 32),  # HP, AP, deck count, discard count
            nn.ReLU()
        )
        
        # Opponent state features
        self.opponent_state_fc = nn.Sequential(
            nn.Linear(5, 32),  # HP, AP, hand count, deck count, discard count
            nn.ReLU()
        )
        
        # Hand features (cards in hand)
        self.hand_conv = nn.Sequential(
            nn.Conv1d(self.state_dim, 64, kernel_size=1),
            nn.ReLU(),
            nn.Conv1d(64, 64, kernel_size=1),
            nn.ReLU()
        )
        self.hand_fc = nn.Sequential(
            nn.Linear(64 * 10, 128),  # 10 is max hand size
            nn.ReLU()
        )
        
        # Status effect features
        self.status_effect_encoder = nn.Sequential(
            nn.Linear(3, 16),  # type, magnitude, duration
            nn.ReLU()
        )
        self.player_status_fc = nn.Sequential(
            nn.Linear(16 * 10, 64),  # 10 is max status effects
            nn.ReLU()
        )
        self.opponent_status_fc = nn.Sequential(
            nn.Linear(16 * 10, 64),  # 10 is max status effects
            nn.ReLU()
        )
        
        # Turn information
        self.turn_fc = nn.Sequential(
            nn.Linear(1, 8),
            nn.ReLU()
        )
        
        # Combined features
        combined_dim = 32 + 32 + 128 + 64 + 64 + 8
        
        # Shared layers
        self.shared_layers = nn.Sequential(
            nn.Linear(combined_dim, self.hidden_dim),
            nn.ReLU(),
            nn.Linear(self.hidden_dim, self.hidden_dim),
            nn.ReLU()
        )
        
        # Policy head (actor)
        self.policy_head = nn.Sequential(
            nn.Linear(self.hidden_dim, self.hidden_dim // 2),
            nn.ReLU(),
            nn.Linear(self.hidden_dim // 2, action_dim)
        )
        
        # Value head (critic)
        self.value_head = nn.Sequential(
            nn.Linear(self.hidden_dim, self.hidden_dim // 2),
            nn.ReLU(),
            nn.Linear(self.hidden_dim // 2, 1)
        )
        
        # Initialize weights
        self.apply(self._init_weights)
    
    def _init_weights(self, module: nn.Module) -> None:
        """
        Initialize weights for the network.
        
        Args:
            module: Module to initialize
        """
        if isinstance(module, (nn.Linear, nn.Conv1d)):
            nn.init.orthogonal_(module.weight, gain=1)
            if module.bias is not None:
                nn.init.constant_(module.bias, 0)
    
    def _extract_features(self, state: Dict[str, torch.Tensor]) -> torch.Tensor:
        """
        Extract features from the state.
        
        Args:
            state: State dictionary
            
        Returns:
            Extracted features
        """
        # Player state features
        player_state = torch.cat([
            state["player_hp"],
            state["player_ap"],
            state["player_deck_count"],
            state["player_discard_count"]
        ], dim=1)
        player_features = self.player_state_fc(player_state)
        
        # Opponent state features
        opponent_state = torch.cat([
            state["opponent_hp"],
            state["opponent_ap"],
            state["opponent_hand_count"],
            state["opponent_deck_count"],
            state["opponent_discard_count"]
        ], dim=1)
        opponent_features = self.opponent_state_fc(opponent_state)
        
        # Hand features
        hand = state["hand"]  # [batch_size, max_hand_size, state_dim]
        batch_size = hand.size(0)
        hand = hand.transpose(1, 2)  # [batch_size, state_dim, max_hand_size]
        hand_conv = self.hand_conv(hand)  # [batch_size, 64, max_hand_size]
        hand_flat = hand_conv.reshape(batch_size, -1)  # [batch_size, 64 * max_hand_size]
        hand_features = self.hand_fc(hand_flat)
        
        # Status effect features
        player_status = state["player_status_effects"]  # [batch_size, 10, 3]
        opponent_status = state["opponent_status_effects"]  # [batch_size, 10, 3]
        
        # Process each status effect
        player_status_encoded = []
        opponent_status_encoded = []
        
        for i in range(10):  # 10 is max status effects
            player_status_i = player_status[:, i, :]  # [batch_size, 3]
            opponent_status_i = opponent_status[:, i, :]  # [batch_size, 3]
            
            player_status_encoded.append(self.status_effect_encoder(player_status_i))
            opponent_status_encoded.append(self.status_effect_encoder(opponent_status_i))
        
        player_status_encoded = torch.cat(player_status_encoded, dim=1)  # [batch_size, 16 * 10]
        opponent_status_encoded = torch.cat(opponent_status_encoded, dim=1)  # [batch_size, 16 * 10]
        
        player_status_features = self.player_status_fc(player_status_encoded)
        opponent_status_features = self.opponent_status_fc(opponent_status_encoded)
        
        # Turn information
        turn_features = self.turn_fc(state["is_my_turn"])
        
        # Combine all features
        combined_features = torch.cat([
            player_features,
            opponent_features,
            hand_features,
            player_status_features,
            opponent_status_features,
            turn_features
        ], dim=1)
        
        return combined_features
    
    def forward(self, state: Dict[str, torch.Tensor]) -> Tuple[torch.Tensor, torch.Tensor]:
        """
        Forward pass through the network.
        
        Args:
            state: State dictionary
            
        Returns:
            Policy logits and value estimate
        """
        # Extract features
        features = self._extract_features(state)
        
        # Shared layers
        shared = self.shared_layers(features)
        
        # Policy head (actor)
        policy_logits = self.policy_head(shared)
        
        # Value head (critic)
        value = self.value_head(shared)
        
        return policy_logits, value
    
    def get_action_and_value(self, state: Dict[str, torch.Tensor], 
                            deterministic: bool = False) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        """
        Get action and value from state.
        
        Args:
            state: State dictionary
            deterministic: Whether to use deterministic action selection
            
        Returns:
            Action, log probability, and value estimate
        """
        # Forward pass
        policy_logits, value = self(state)
        
        # Get action distribution
        action_probs = F.softmax(policy_logits, dim=1)
        action_dist = torch.distributions.Categorical(action_probs)
        
        # Sample action
        if deterministic:
            action = torch.argmax(action_probs, dim=1)
        else:
            action = action_dist.sample()
        
        # Get log probability
        action_log_prob = action_dist.log_prob(action)
        
        return action, action_log_prob, value
    
    def evaluate_actions(self, state: Dict[str, torch.Tensor], 
                        actions: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        """
        Evaluate actions given state.
        
        Args:
            state: State dictionary
            actions: Actions to evaluate
            
        Returns:
            Action log probabilities, value estimates, and entropy
        """
        # Forward pass
        policy_logits, value = self(state)
        
        # Get action distribution
        action_probs = F.softmax(policy_logits, dim=1)
        action_dist = torch.distributions.Categorical(action_probs)
        
        # Get log probability
        action_log_probs = action_dist.log_prob(actions)
        
        # Get entropy
        entropy = action_dist.entropy().mean()
        
        return action_log_probs, value, entropy
