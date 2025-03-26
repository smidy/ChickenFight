import torch
import torch.nn as nn
import torch.nn.functional as F
import numpy as np
from typing import Dict, List, Tuple, Union, Optional

class CardEncoder(nn.Module):
    """
    Encodes card information into a fixed-size representation.
    """
    def __init__(self, embedding_dim: int = 64, card_feature_dim: int = 32):
        super(CardEncoder, self).__init__()
        # Card type embedding
        self.type_embedding = nn.Embedding(4, embedding_dim // 4)  # 4 card types
        
        # Card features network (cost, description, etc)
        self.card_features = nn.Sequential(
            nn.Linear(4, card_feature_dim),  # Cost + other numeric features
            nn.ReLU(),
            nn.Linear(card_feature_dim, embedding_dim // 2),
            nn.ReLU()
        )
        
        # Final projection
        self.projection = nn.Linear(embedding_dim // 4 + embedding_dim // 2, embedding_dim)
    
    def forward(self, card_data: Dict) -> torch.Tensor:
        """
        Encode a card into a fixed-size vector.
        
        Args:
            card_data: Dictionary containing card information
                - 'Id': Card ID
                - 'Name': Card name
                - 'Description': Card description
                - 'Cost': Card cost in action points
        
        Returns:
            torch.Tensor: Encoded card representation
        """
        batch_size = len(card_data) if isinstance(card_data, list) else 1
        
        # Process a single card
        if not isinstance(card_data, list):
            card_data = [card_data]
        
        # Extract card types from descriptions or names
        # For simplicity, we'll just use the first character of card ID to determine type
        # In production, you'd use a more robust method
        card_types = []
        for card in card_data:
            if card is None:
                card_types.append(0)  # Default type
                continue
                
            card_id = card.get('Id', '')
            if card_id.startswith('atk'):
                card_types.append(0)  # Attack
            elif card_id.startswith('def'):
                card_types.append(1)  # Defense
            elif card_id.startswith('spc'):
                card_types.append(2)  # Special
            elif card_id.startswith('utl'):
                card_types.append(3)  # Utility
            else:
                card_types.append(0)  # Default to attack
        
        # Convert to tensor
        card_types = torch.tensor(card_types, dtype=torch.long, device=next(self.parameters()).device)
        
        # Get type embeddings
        type_emb = self.type_embedding(card_types)
        
        # Extract numeric features
        features = []
        for card in card_data:
            if card is None:
                features.append([0, 0, 0, 0])  # Default features
                continue
                
            cost = card.get('Cost', 0)
            # Placeholder for additional numeric features
            # You could extract more features from the card description if needed
            feature = [cost, 0, 0, 0]  # Cost + placeholder features
            features.append(feature)
            
        # Convert to tensor
        features = torch.tensor(features, dtype=torch.float, device=next(self.parameters()).device)
        
        # Process through feature network
        feature_emb = self.card_features(features)
        
        # Concatenate embeddings
        combined = torch.cat([type_emb, feature_emb], dim=1)
        
        # Final projection
        output = self.projection(combined)
        
        return output

class A2CModel(nn.Module):
    """
    Actor-Critic model for the card battle game.
    """
    def __init__(self, 
                 state_dim: int = 140,  # Updated to match actual state tensor dimension
                 action_dim: int = 16,
                 card_embedding_dim: int = 64,
                 hidden_dim: int = 256):
        super(A2CModel, self).__init__()
        
        # Card encoder
        self.card_encoder = CardEncoder(embedding_dim=card_embedding_dim)
        
        # State encoder
        self.state_encoder = nn.Sequential(
            nn.Linear(state_dim, hidden_dim),
            nn.ReLU(),
            nn.Linear(hidden_dim, hidden_dim),
            nn.ReLU()
        )
        
        # LSTM for sequential decision making
        self.lstm = nn.LSTM(hidden_dim, hidden_dim, batch_first=True)
        
        # Actor (policy) network
        self.actor = nn.Sequential(
            nn.Linear(hidden_dim, hidden_dim),
            nn.ReLU(),
            nn.Linear(hidden_dim, action_dim)
        )
        
        # Critic (value) network
        self.critic = nn.Sequential(
            nn.Linear(hidden_dim, hidden_dim),
            nn.ReLU(),
            nn.Linear(hidden_dim, 1)
        )
        
        # Initialize hidden state for LSTM
        self.hidden = None
    
    def _encode_cards(self, cards: List[Dict]) -> torch.Tensor:
        """
        Encode a list of cards into a fixed-size tensor.
        
        Args:
            cards: List of card dictionaries
            
        Returns:
            torch.Tensor: Encoded cards representation with shape [1, embedding_dim]
        """
        if not cards:
            # Return zeros if no cards
            return torch.zeros(1, self.card_encoder.projection.out_features, 
                              device=next(self.parameters()).device)
        
        # Encode each card
        card_embeddings = []
        for card in cards:
            embedding = self.card_encoder(card)
            # Ensure embedding is 2D with shape [1, embedding_dim]
            if embedding.dim() > 2:
                embedding = embedding.view(1, -1)
            card_embeddings.append(embedding)
        
        # Stack embeddings and compute mean
        stacked = torch.stack(card_embeddings, dim=0)
        output = stacked.mean(dim=0, keepdim=True)
        
        # Ensure output has shape [1, embedding_dim]
        if output.dim() != 2:
            output = output.view(1, -1)
            
        return output
    
    def _encode_state(self, state: Dict) -> torch.Tensor:
        """
        Encode the game state into a fixed-size tensor.
        
        Args:
            state: Dictionary containing game state
            
        Returns:
            torch.Tensor: Encoded state representation
        """
        # Create a list of tensors to represent the state
        tensors = []
        device = next(self.parameters()).device
        
        # Flag for being in a fight
        is_in_fight = float(state.get('is_in_fight', False))
        tensors.append(torch.tensor([[is_in_fight]], dtype=torch.float, device=device))
        
        if is_in_fight:
            # It's a card battle state
            
            # Flag for player's turn
            is_player_turn = float(state.get('is_player_turn', False))
            tensors.append(torch.tensor([[is_player_turn]], dtype=torch.float, device=device))
            
            # HP values (normalized)
            player_hp = state.get('player_hit_points', 50) / 50.0
            opponent_hp = state.get('opponent_hit_points', 50) / 50.0
            tensors.append(torch.tensor([[player_hp, opponent_hp]], dtype=torch.float, device=device))
            
            # AP values (normalized by estimating max at 15)
            player_ap = state.get('player_action_points', 0) / 15.0
            opponent_ap = state.get('opponent_action_points', 0) / 15.0
            tensors.append(torch.tensor([[player_ap, opponent_ap]], dtype=torch.float, device=device))
            
            # Deck counts (normalized by estimating max at 30)
            player_deck = state.get('player_deck_count', 0) / 30.0
            opponent_deck = state.get('opponent_deck_count', 0) / 30.0
            tensors.append(torch.tensor([[player_deck, opponent_deck]], dtype=torch.float, device=device))
            
            # Discard pile counts (normalized by estimating max at 30)
            player_discard = state.get('player_discard_pile_count', 0) / 30.0
            opponent_discard = state.get('opponent_discard_pile_count', 0) / 30.0
            tensors.append(torch.tensor([[player_discard, opponent_discard]], dtype=torch.float, device=device))
            
            # Hand sizes (normalized by estimating max at 10)
            player_hand_size = len(state.get('player_cards_in_hand', [])) / 10.0
            opponent_hand_size = state.get('opponent_cards_in_hand_count', 0) / 10.0
            tensors.append(torch.tensor([[player_hand_size, opponent_hand_size]], dtype=torch.float, device=device))
            
            # Encode the cards in player's hand
            cards_embedding = self._encode_cards(state.get('player_cards_in_hand', []))
            tensors.append(cards_embedding)
            
            # Encode the last played card, if any
            last_card_embedding = self._encode_cards([state.get('last_played_card')] if state.get('last_played_card') else [])
            tensors.append(last_card_embedding)
            
            # Status effects could also be encoded here if needed
            
        else:
            # It's a map state
            # For map state, we'll just use placeholder values to maintain tensor dimensions
            placeholder = torch.zeros(1, 14, device=device)
            tensors.append(placeholder)
            
            # Add empty card embeddings to maintain tensor dimensions
            empty_cards = torch.zeros(1, self.card_encoder.projection.out_features, device=device)
            tensors.append(empty_cards)
            tensors.append(empty_cards)
        
        # Ensure all tensors have the same number of dimensions (2D) before concatenation
        normalized_tensors = []
        for tensor in tensors:
            if tensor.dim() != 2:
                # Reshape to 2D tensor with shape [1, features]
                normalized_tensors.append(tensor.view(1, -1))
            else:
                normalized_tensors.append(tensor)
        
        # Concatenate all tensors
        state_tensor = torch.cat(normalized_tensors, dim=1)
        
        # Pass through state encoder
        return self.state_encoder(state_tensor)
    
    def reset_hidden(self, batch_size: int = 1):
        """
        Reset the hidden state of the LSTM.
        
        Args:
            batch_size: Batch size for the hidden state
        """
        device = next(self.parameters()).device
        h0 = torch.zeros(1, batch_size, self.lstm.hidden_size, device=device)
        c0 = torch.zeros(1, batch_size, self.lstm.hidden_size, device=device)
        self.hidden = (h0, c0)
    
    def forward(self, 
                state: Dict, 
                action_mask: Optional[torch.Tensor] = None) -> Tuple[torch.Tensor, torch.Tensor]:
        """
        Forward pass of the model.
        
        Args:
            state: Dictionary containing game state
            action_mask: Optional tensor indicating which actions are valid
            
        Returns:
            Tuple[torch.Tensor, torch.Tensor]: Policy logits and value estimate
        """
        # Encode state
        state_encoding = self._encode_state(state)
        
        # Add sequence dimension if not present
        if len(state_encoding.shape) == 2:
            state_encoding = state_encoding.unsqueeze(1)
        
        # Initialize hidden state if not done already
        if self.hidden is None:
            self.reset_hidden(state_encoding.size(0))
            
        # LSTM
        lstm_out, self.hidden = self.lstm(state_encoding, self.hidden)
        
        # Remove sequence dimension
        feature = lstm_out.squeeze(1)
        
        # Policy (actor)
        policy_logits = self.actor(feature)
        
        # Apply action mask if provided
        if action_mask is not None:
            # Set logits of invalid actions to a large negative value
            policy_logits = policy_logits.masked_fill(action_mask == 0, -1e9)
        
        # Value (critic)
        value = self.critic(feature)
        
        return policy_logits, value
    
    def to_device(self, device):
        """
        Move the model to the specified device.
        
        Args:
            device: The device to move the model to
        
        Returns:
            A2CModel: The model on the specified device
        """
        self.to(device)
        if self.hidden is not None:
            self.hidden = (self.hidden[0].to(device), self.hidden[1].to(device))
        return self
