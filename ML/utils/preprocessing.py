"""
Preprocessing utilities for the A2C training.

This module provides functions for state preprocessing and normalization,
action space handling, and other utility functions.
"""

import torch
import numpy as np
from typing import Dict, List, Union, Tuple, Any, Optional

def normalize_hp(hp: int, max_hp: int = 50) -> float:
    """
    Normalize hit points to [0, 1] range.
    
    Args:
        hp: Hit points value
        max_hp: Maximum possible hit points
    
    Returns:
        float: Normalized HP value
    """
    return max(0.0, min(1.0, hp / max_hp))

def normalize_ap(ap: int, max_ap: int = 15) -> float:
    """
    Normalize action points to [0, 1] range.
    
    Args:
        ap: Action points value
        max_ap: Estimated maximum action points
    
    Returns:
        float: Normalized AP value
    """
    return max(0.0, min(1.0, ap / max_ap))

def normalize_deck_count(count: int, max_count: int = 30) -> float:
    """
    Normalize deck/discard counts to [0, 1] range.
    
    Args:
        count: Deck or discard pile count
        max_count: Estimated maximum count
    
    Returns:
        float: Normalized count value
    """
    return max(0.0, min(1.0, count / max_count))

def normalize_hand_size(size: int, max_size: int = 10) -> float:
    """
    Normalize hand size to [0, 1] range.
    
    Args:
        size: Hand size
        max_size: Maximum possible hand size
    
    Returns:
        float: Normalized hand size value
    """
    return max(0.0, min(1.0, size / max_size))

def preprocess_card(card: Dict) -> Dict:
    """
    Preprocess a card dictionary to extract relevant features.
    
    Args:
        card: Card information dictionary
    
    Returns:
        Dict: Processed card features
    """
    if card is None:
        return {
            'id': '',
            'type': 'unknown',
            'cost': 0,
            'normalized_cost': 0.0,
            'is_attack': 0.0,
            'is_defense': 0.0,
            'is_special': 0.0,
            'is_utility': 0.0
        }
    
    card_id = card.get('Id', '')
    card_cost = card.get('Cost', 0)
    
    # Determine card type from ID
    is_attack = 1.0 if card_id.startswith('atk') else 0.0
    is_defense = 1.0 if card_id.startswith('def') else 0.0
    is_special = 1.0 if card_id.startswith('spc') else 0.0
    is_utility = 1.0 if card_id.startswith('utl') else 0.0
    
    # Default to 'unknown' if none match
    card_type = 'unknown'
    if is_attack > 0:
        card_type = 'attack'
    elif is_defense > 0:
        card_type = 'defense'
    elif is_special > 0:
        card_type = 'special'
    elif is_utility > 0:
        card_type = 'utility'
    
    return {
        'id': card_id,
        'type': card_type,
        'cost': card_cost,
        'normalized_cost': normalize_ap(card_cost),
        'is_attack': is_attack,
        'is_defense': is_defense,
        'is_special': is_special,
        'is_utility': is_utility
    }

def preprocess_state(state: Dict) -> Dict:
    """
    Preprocess a game state dictionary for the neural network.
    
    Args:
        state: Raw game state dictionary
    
    Returns:
        Dict: Processed state features
    """
    is_in_fight = state.get('is_in_fight', False)
    
    if not is_in_fight:
        # Map-level state
        return {
            'is_in_fight': 0.0,
            'map_features': {
                'player_id': state.get('player_id', ''),
                'player_position': state.get('player_position', None),
                'other_players_count': len(state.get('other_players', {})),
                'available_opponents_count': sum(
                    1 for p, info in state.get('other_players', {}).items() 
                    if not info.get('in_fight', False)
                )
            }
        }
    
    # Process card battle state
    processed_state = {
        'is_in_fight': 1.0,
        'is_player_turn': 1.0 if state.get('is_player_turn', False) else 0.0,
        'player_id': state.get('player_id', ''),
        'opponent_id': state.get('opponent_id', ''),
        
        # Normalized values
        'player_hp_norm': normalize_hp(state.get('player_hit_points', 50)),
        'opponent_hp_norm': normalize_hp(state.get('opponent_hit_points', 50)),
        'player_ap_norm': normalize_ap(state.get('player_action_points', 0)),
        'opponent_ap_norm': normalize_ap(state.get('opponent_action_points', 0)),
        'player_deck_norm': normalize_deck_count(state.get('player_deck_count', 0)),
        'opponent_deck_norm': normalize_deck_count(state.get('opponent_deck_count', 0)),
        'player_discard_norm': normalize_deck_count(state.get('player_discard_pile_count', 0)),
        'opponent_discard_norm': normalize_deck_count(state.get('opponent_discard_pile_count', 0)),
        'player_hand_size_norm': normalize_hand_size(len(state.get('player_cards_in_hand', []))),
        'opponent_hand_size_norm': normalize_hand_size(state.get('opponent_cards_in_hand_count', 0)),
        
        # Raw values (for reference)
        'player_hp': state.get('player_hit_points', 50),
        'opponent_hp': state.get('opponent_hit_points', 50),
        'player_ap': state.get('player_action_points', 0),
        'opponent_ap': state.get('opponent_action_points', 0),
        'player_deck_count': state.get('player_deck_count', 0),
        'opponent_deck_count': state.get('opponent_deck_count', 0),
        'player_discard_count': state.get('player_discard_pile_count', 0),
        'opponent_discard_count': state.get('opponent_discard_pile_count', 0),
        
        # Processed cards
        'player_cards': [preprocess_card(card) for card in state.get('player_cards_in_hand', [])],
        'last_played_card': preprocess_card(state.get('last_played_card'))
    }
    
    # Process status effects
    player_status_effects = state.get('player_status_effects', [])
    opponent_status_effects = state.get('opponent_status_effects', [])
    
    processed_state['player_status_effect_count'] = len(player_status_effects)
    processed_state['opponent_status_effect_count'] = len(opponent_status_effects)
    
    return processed_state

def calculate_advantage(returns: torch.Tensor, values: torch.Tensor) -> torch.Tensor:
    """
    Calculate advantage values from returns and value estimates.
    
    Args:
        returns: Return values (discounted future rewards)
        values: Value estimates from critic network
    
    Returns:
        torch.Tensor: Advantage values
    """
    return returns - values

def create_action_mask_from_valid_actions(valid_actions: Dict, action_dim: int) -> torch.Tensor:
    """
    Create a binary mask for valid actions.
    
    Args:
        valid_actions: Dictionary of valid actions
        action_dim: Size of action space
    
    Returns:
        torch.Tensor: Binary mask where 1 indicates valid action
    """
    mask = torch.zeros(action_dim)
    
    if valid_actions.get('type') == 'map':
        # Map actions (challenge player)
        available_players = valid_actions.get('available_players', [])
        for i, _ in enumerate(available_players[:min(10, action_dim-1)]):
            mask[i] = 1
            
    elif valid_actions.get('type') == 'battle':
        # Battle actions
        if valid_actions.get('can_end_turn', False):
            # Last action is end turn
            mask[action_dim-1] = 1
            
        # Card play actions
        playable_cards = valid_actions.get('playable_cards', [])
        for i, _ in enumerate(playable_cards[:min(len(playable_cards), action_dim-1)]):
            mask[i] = 1
    
    return mask

def action_to_game_action(action_idx: int, valid_actions: Dict, action_dim: int) -> Dict:
    """
    Convert model action index to game action.
    
    Args:
        action_idx: Index of the selected action
        valid_actions: Dictionary of valid actions
        action_dim: Size of action space
        
    Returns:
        Dict: Game action representation
    """
    if valid_actions.get('type') == 'map':
        # Map actions
        available_players = valid_actions.get('available_players', [])
        if action_idx < len(available_players):
            # Challenge player
            return {
                'type': 'challenge',
                'target_id': available_players[action_idx]
            }
        else:
            # Wait
            return {
                'type': 'wait'
            }
            
    elif valid_actions.get('type') == 'battle':
        # Battle actions
        if action_idx == action_dim - 1:
            # End turn
            return {
                'type': 'end_turn'
            }
        else:
            # Play card
            playable_cards = valid_actions.get('playable_cards', [])
            if action_idx < len(playable_cards):
                return {
                    'type': 'play_card',
                    'card_id': playable_cards[action_idx].get('Id')
                }
            else:
                # Default to end turn if invalid index
                return {
                    'type': 'end_turn'
                }
                
    # Default action
    return {
        'type': 'wait'
    }
