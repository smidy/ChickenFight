import asyncio
import json
import websockets
from typing import Dict, List, Optional, Any, Callable, Union, Tuple
from enum import Enum

class GameContext:
    """
    Python equivalent of the C# GameStateContext class.
    Client-side game state that keeps track of player, map, fight, and card battle state.
    Processes incoming server messages to update the state.
    
    Enhanced with Reinforcement Learning capabilities for A2C training.
    """
    
    def __init__(self, server_url: str):
        """
        Initializes a new instance of the GameContext class
        
        Args:
            server_url: WebSocket URL of the game server
        """
        self._disposed = False
        self._websocket = None
        self._server_url = server_url
        self._on_server_message_callbacks = []
        
        # Player data
        self.player_id = None
        self.player_position = None
        
        # Map data
        self.current_map_id = None
        self.current_tilemap_data = None
        
        # Other players
        self.other_players = {}  # Dict[str, MapPosition]
        self.other_player_info = {}  # Dict[str, PlayerMapInfo]
        self.players_in_fight = {}  # Dict[str, bool]
        
        # Fight state
        self.current_fight_id = None
        self.opponent_id = None
        
        # Card battle state
        self.card_svg_data = {}  # Dict[str, str]
        self.cards_in_hand = []  # List[CardInfo]
        self.player_hit_points = 50
        self.player_action_points = 0
        self.player_deck_count = 0
        self.player_discard_pile_count = 0
        self.opponent_hit_points = 50
        self.opponent_action_points = 0
        self.opponent_deck_count = 0
        self.opponent_discard_pile_count = 0
        self.opponent_cards_in_hand = []  # List[CardInfo]
        self.player_status_effects = []  # List[StatusEffectInfo]
        self.opponent_status_effects = []  # List[StatusEffectInfo]
        self.last_played_card = None
        self.current_turn_player_id = None
        
        # Pending operations
        self.pending_move = None
        
    @property
    def is_in_fight(self) -> bool:
        """Returns True if the player is currently in a fight"""
        return self.current_fight_id is not None
    
    @property
    def is_moving(self) -> bool:
        """Returns True if the player has a pending move"""
        return self.pending_move is not None
    
    @property
    def is_player_turn(self) -> bool:
        """Returns True if it's currently the player's turn in a card battle"""
        return self.current_turn_player_id == self.player_id
    
    async def connect(self):
        """Establishes a WebSocket connection to the server"""
        self._websocket = await websockets.connect(self._server_url)
        # Set up message listener
        asyncio.create_task(self._listen_for_messages())
    
    async def _listen_for_messages(self):
        """Listens for incoming WebSocket messages and processes them"""
        try:
            async for message in self._websocket:
                ext_server_message = json.loads(message)
                self.on_receive(ext_server_message)
                
                # Notify any registered callbacks
                for callback in self._on_server_message_callbacks:
                    callback(ext_server_message)
        except websockets.exceptions.ConnectionClosed:
            # Handle connection closed
            print("WebSocket connection closed")
        except Exception as e:
            # Handle other errors
            print(f"Error in WebSocket listener: {e}")
    
    def add_server_message_callback(self, callback: Callable):
        """
        Adds a callback function that will be called when a server message is received
        
        Args:
            callback: Function that takes a message as its parameter
        """
        self._on_server_message_callbacks.append(callback)
    
    def remove_server_message_callback(self, callback: Callable):
        """
        Removes a previously added callback function
        
        Args:
            callback: Function to remove
        """
        if callback in self._on_server_message_callbacks:
            self._on_server_message_callbacks.remove(callback)
    
    async def send(self, message: Dict):
        """
        Serializes and sends a message to the server
        
        Args:
            message: The message to send
            
        Returns:
            bool: True if the message was sent successfully
        """
        if not self._websocket:
            return False
        
        try:
            serialized_message = json.dumps(message)
            await self._websocket.send(serialized_message)
            return True
        except Exception as e:
            print(f"Error sending message: {e}")
            return False
    
    def on_receive(self, ext_server_message: Dict):
        """
        Processes an incoming server message and updates the game state accordingly
        
        Args:
            ext_server_message: The server message to process
            
        Raises:
            NotImplementedError: When a message type is not handled
        """
        message_type = ext_server_message.get("MessageType")
        
        # Connection messages
        if message_type == "ExtPlayerIdResponse":
            self._on_ext_player_id_response(ext_server_message)
        
        # Map messages
        elif message_type == "ExtMapListResponse":
            # Map list doesn't affect game state
            pass
        elif message_type == "ExtJoinMapInitiated":
            self._on_ext_join_map_initiated(ext_server_message)
        elif message_type == "ExtJoinMapCompleted":
            self._on_ext_join_map_completed(ext_server_message)
        elif message_type == "ExtJoinMapFailed":
            # Handle join map failure
            pass
        elif message_type == "ExtLeaveMapInitiated":
            self._on_ext_leave_map_initiated(ext_server_message)
        elif message_type == "ExtLeaveMapCompleted":
            self._on_ext_leave_map_completed(ext_server_message)
        elif message_type == "ExtLeaveMapFailed":
            # Handle leave map failure
            pass
        elif message_type == "ExtPlayerJoinedMap":
            self._on_ext_player_joined_map(ext_server_message)
        elif message_type == "ExtPlayerLeftMap":
            self._on_ext_player_left_map(ext_server_message)
        elif message_type == "ExtPlayerPositionChange":
            self._on_ext_player_position_change(ext_server_message)
        
        # Movement messages
        elif message_type == "ExtMoveInitiated":
            self._on_ext_move_initiated(ext_server_message)
        elif message_type == "ExtMoveCompleted":
            self._on_ext_move_completed(ext_server_message)
        elif message_type == "ExtMoveFailed":
            self._on_ext_move_failed(ext_server_message)
        
        # Fight messages
        elif message_type == "ExtFightChallengeReceived":
            # Challenge received doesn't affect state until accepted
            pass
        elif message_type == "ExtFightStarted":
            self._on_ext_fight_started(ext_server_message)
        elif message_type == "ExtFightEnded":
            self._on_ext_fight_ended(ext_server_message)
        
        # Card battle messages
        elif message_type == "ExtCardImages":
            self._on_ext_card_images(ext_server_message)
        elif message_type == "ExtCardDrawn":
            self._on_ext_card_drawn(ext_server_message)
        elif message_type == "ExtTurnStarted":
            self._on_ext_turn_started(ext_server_message)
        elif message_type == "ExtTurnEnded":
            self._on_ext_turn_ended(ext_server_message)
        elif message_type == "ExtCardPlayInitiated":
            # Initiated doesn't affect state until completed
            pass
        elif message_type == "ExtCardPlayCompleted":
            self._on_ext_card_play_completed(ext_server_message)
        elif message_type == "ExtCardPlayFailed":
            self._on_ext_card_play_failed(ext_server_message)
        elif message_type == "ExtEffectApplied":
            self._on_ext_effect_applied(ext_server_message)
        elif message_type == "ExtFightStateUpdate":
            self._on_ext_fight_state_update(ext_server_message)
        
        else:
            raise NotImplementedError(f"Message type {message_type} not handled")
    
    def reset(self):
        """Resets the game state to its initial values"""
        # Reset map data
        self.current_map_id = None
        self.current_tilemap_data = None
        
        # Reset player data
        self.player_position = None
        self.pending_move = None
        
        # Reset other players
        self.other_players.clear()
        self.other_player_info.clear()
        self.players_in_fight.clear()
        
        # Reset fight state
        self.current_fight_id = None
        self.opponent_id = None
        
        # Reset card battle state
        self.card_svg_data.clear()
        self.cards_in_hand.clear()
        self.player_hit_points = 50
        self.player_action_points = 0
        self.player_deck_count = 0
        self.player_discard_pile_count = 0
        self.opponent_hit_points = 50
        self.opponent_action_points = 0
        self.opponent_deck_count = 0
        self.opponent_discard_pile_count = 0
        self.opponent_cards_in_hand.clear()
        self.player_status_effects.clear()
        self.opponent_status_effects.clear()
        self.last_played_card = None
        self.current_turn_player_id = None
    
    def add_player(self, player_id: str, player_info: Dict):
        """
        Adds a player to the list of other players
        
        Args:
            player_id: The ID of the player to add
            player_info: Information about the player
        """
        if player_id != self.player_id:
            # Store the player info
            self.other_player_info[player_id] = player_info
            
            # Extract position for backward compatibility
            self.other_players[player_id] = player_info["Position"]
            
            # If the player is in a fight, add them to the players_in_fight dictionary
            if player_info.get("FightId"):
                self.players_in_fight[player_id] = True
    
    def update_player_position(self, player_id: str, position: Dict):
        """
        Updates the position of a player in the list of other players
        
        Args:
            player_id: The ID of the player to update
            position: The new position of the player
        """
        if player_id != self.player_id and player_id in self.other_players:
            self.other_players[player_id] = position
            
            # Also update the position in the PlayerMapInfo
            if player_id in self.other_player_info:
                current_info = self.other_player_info[player_id]
                updated_info = {
                    "Position": position,
                    "FightId": current_info.get("FightId")
                }
                self.other_player_info[player_id] = updated_info
    
    def remove_player(self, player_id: str):
        """
        Removes a player from the list of other players
        
        Args:
            player_id: The ID of the player to remove
        """
        self.other_players.pop(player_id, None)
        self.other_player_info.pop(player_id, None)
        self.players_in_fight.pop(player_id, None)
    
    # Connection Message Handlers
    
    def _on_ext_player_id_response(self, msg: Dict):
        """Handler for ExtPlayerIdResponse message"""
        self.player_id = msg.get("PlayerId")
    
    # Map Message Handlers
    
    def _on_ext_join_map_initiated(self, msg: Dict):
        """Handler for ExtJoinMapInitiated message"""
        # Store the map ID
        self.current_map_id = msg.get("MapId")
    
    def _on_ext_join_map_completed(self, msg: Dict):
        """Handler for ExtJoinMapCompleted message"""
        # Set map data
        self.current_map_id = msg.get("MapId")
        self.current_tilemap_data = msg.get("TilemapData")
        
        # Set player position
        self.player_position = msg.get("Position")
        
        # Add other players
        self.other_players.clear()
        self.other_player_info.clear()
        self.players_in_fight.clear()
        
        player_info = msg.get("PlayerInfo", {})
        for key, value in player_info.items():
            if key != self.player_id:
                self.add_player(key, value)
    
    def _on_ext_leave_map_initiated(self, msg: Dict):
        """Handler for ExtLeaveMapInitiated message"""
        # No state changes needed
        pass
    
    def _on_ext_leave_map_completed(self, msg: Dict):
        """Handler for ExtLeaveMapCompleted message"""
        # Clear map data
        self.current_map_id = None
        self.current_tilemap_data = None
        
        # Clear player position
        self.player_position = None
        
        # Clear other players
        self.other_players.clear()
        self.other_player_info.clear()
        self.players_in_fight.clear()
    
    def _on_ext_player_joined_map(self, msg: Dict):
        """Handler for ExtPlayerJoinedMap message"""
        player_id = msg.get("PlayerId")
        position = msg.get("Position")
        
        if player_id != self.player_id and position is not None:
            # Create player info
            player_info = {
                "Position": position,
                "FightId": None
            }
            
            # Add player to collections
            self.add_player(player_id, player_info)
    
    def _on_ext_player_left_map(self, msg: Dict):
        """Handler for ExtPlayerLeftMap message"""
        player_id = msg.get("PlayerId")
        
        if player_id != self.player_id:
            self.remove_player(player_id)
    
    def _on_ext_player_position_change(self, msg: Dict):
        """Handler for ExtPlayerPositionChange message"""
        player_id = msg.get("PlayerId")
        position = msg.get("Position")
        
        if player_id == self.player_id and position is not None:
            # Update player position
            self.player_position = position
        elif position is not None:
            # Update other player position
            self.update_player_position(player_id, position)
    
    # Movement Message Handlers
    
    def _on_ext_move_initiated(self, msg: Dict):
        """Handler for ExtMoveInitiated message"""
        self.pending_move = msg.get("NewPosition")
    
    def _on_ext_move_completed(self, msg: Dict):
        """Handler for ExtMoveCompleted message"""
        self.player_position = msg.get("NewPosition")
        self.pending_move = None
    
    def _on_ext_move_failed(self, msg: Dict):
        """Handler for ExtMoveFailed message"""
        self.pending_move = None
    
    # Fight Message Handlers
    
    def _on_ext_fight_started(self, msg: Dict):
        """Handler for ExtFightStarted message"""
        player1_id = msg.get("Player1Id")
        player2_id = msg.get("Player2Id")
        
        # Generate a fight ID
        fight_id = f"fight_{player1_id}_{player2_id}"
        
        # Mark both players as in a fight
        self.players_in_fight[player1_id] = True
        self.players_in_fight[player2_id] = True
        
        # Update the fight ID in other_player_info for both players
        if player1_id in self.other_player_info:
            current_info = self.other_player_info[player1_id]
            updated_info = {
                "Position": current_info.get("Position"),
                "FightId": fight_id
            }
            self.other_player_info[player1_id] = updated_info
        
        if player2_id in self.other_player_info:
            current_info = self.other_player_info[player2_id]
            updated_info = {
                "Position": current_info.get("Position"),
                "FightId": fight_id
            }
            self.other_player_info[player2_id] = updated_info
        
        # Set fight state for the main player if they're involved
        if player1_id == self.player_id or player2_id == self.player_id:
            self.current_fight_id = fight_id
            self.opponent_id = player2_id if player1_id == self.player_id else player1_id
            
            # Reset card battle state for new fight
            self.card_svg_data.clear()
            self.cards_in_hand.clear()
            self.player_hit_points = 50
            self.player_action_points = 0
            self.player_deck_count = 0
            self.player_discard_pile_count = 0
            self.opponent_hit_points = 50
            self.opponent_action_points = 0
            self.opponent_deck_count = 0
            self.opponent_discard_pile_count = 0
            self.opponent_cards_in_hand.clear()
            self.player_status_effects.clear()
            self.opponent_status_effects.clear()
            self.current_turn_player_id = None
    
    def _on_ext_fight_ended(self, msg: Dict):
        """Handler for ExtFightEnded message"""
        winner_id = msg.get("WinnerId")
        loser_id = msg.get("LoserId")
        reason = msg.get("Reason")
        
        # Special handling for disconnection
        if reason == "Player disconnected":
            # Remove the disconnected player from the players_in_fight dictionary
            self.players_in_fight.pop(loser_id, None)
            
            # Also remove from other_players and other_player_info
            self.other_players.pop(loser_id, None)
            self.other_player_info.pop(loser_id, None)
        else:
            # Normal cleanup for both players
            self.players_in_fight.pop(winner_id, None)
            self.players_in_fight.pop(loser_id, None)
            
            # Clear fight IDs in other_player_info
            if winner_id in self.other_player_info:
                current_info = self.other_player_info[winner_id]
                updated_info = {
                    "Position": current_info.get("Position"),
                    "FightId": None
                }
                self.other_player_info[winner_id] = updated_info
            
            if loser_id in self.other_player_info:
                current_info = self.other_player_info[loser_id]
                updated_info = {
                    "Position": current_info.get("Position"),
                    "FightId": None
                }
                self.other_player_info[loser_id] = updated_info
        
        # Reset fight state if the main player was involved
        if winner_id == self.player_id or loser_id == self.player_id:
            self.current_fight_id = None
            self.opponent_id = None
            
            # Reset card battle state
            self.card_svg_data.clear()
            self.cards_in_hand.clear()
            self.player_hit_points = 50
            self.player_action_points = 0
            self.player_deck_count = 0
            self.player_discard_pile_count = 0
            self.opponent_hit_points = 50
            self.opponent_action_points = 0
            self.opponent_deck_count = 0
            self.opponent_discard_pile_count = 0
            self.opponent_cards_in_hand.clear()
            self.player_status_effects.clear()
            self.opponent_status_effects.clear()
            self.current_turn_player_id = None
    
    # Card Battle Message Handlers
    
    def _on_ext_card_images(self, msg: Dict):
        """Handler for ExtCardImages message"""
        card_svg_data = msg.get("CardSvgData", {})
        for key, value in card_svg_data.items():
            self.card_svg_data[key] = value
    
    def _on_ext_card_drawn(self, msg: Dict):
        """Handler for ExtCardDrawn message"""
        # Only update the SVG data cache, don't modify the hand
        card_info = msg.get("CardInfo", {})
        card_id = card_info.get("Id")
        svg_data = msg.get("SvgData")
        
        if card_id and svg_data:
            self.card_svg_data[card_id] = svg_data
        
        # Hand updates are now handled by _on_ext_fight_state_update
    
    def _on_ext_turn_started(self, msg: Dict):
        """Handler for ExtTurnStarted message"""
        self.current_turn_player_id = msg.get("ActivePlayerId")
        
        # Card handling is now managed by _on_ext_fight_state_update
    
    def _on_ext_turn_ended(self, msg: Dict):
        """Handler for ExtTurnEnded message"""
        player_id = msg.get("PlayerId")
        
        if player_id == self.player_id:
            # Player's turn ended
            self.current_turn_player_id = self.opponent_id
            
            # Clear player's hand as it's moved to discard pile
            self.cards_in_hand.clear()
        else:
            # Opponent's turn ended
            self.current_turn_player_id = self.player_id
            
            # Clear opponent's hand as it's moved to discard pile
            self.opponent_cards_in_hand.clear()
    
    def _on_ext_card_play_completed(self, msg: Dict):
        """Handler for ExtCardPlayCompleted message"""
        player_id = msg.get("PlayerId")
        played_card = msg.get("PlayedCard", {})
        card_id = played_card.get("Id")
        
        # Store the last played card
        self.last_played_card = played_card
        
        if player_id == self.player_id:
            # Remove the card from the player's hand
            self.cards_in_hand = [card for card in self.cards_in_hand if card.get("Id") != card_id]
        else:
            # Remove the card from the opponent's hand
            self.opponent_cards_in_hand = [card for card in self.opponent_cards_in_hand if card.get("Id") != card_id]
    
    def _on_ext_card_play_failed(self, msg: Dict):
        """Handler for ExtCardPlayFailed message"""
        # Handle card play failure (log or other handling)
        pass
    
    def _on_ext_effect_applied(self, msg: Dict):
        """Handler for ExtEffectApplied message"""
        target_player_id = msg.get("TargetPlayerId")
        effect_type = msg.get("EffectType")
        value = msg.get("Value", 0)
        
        # Update player stats based on effect
        if target_player_id == self.player_id:
            if effect_type == "Damage":
                self.player_hit_points = max(0, self.player_hit_points - value)
            elif effect_type == "Heal":
                self.player_hit_points = min(50, self.player_hit_points + value)
        else:
            if effect_type == "Damage":
                self.opponent_hit_points = max(0, self.opponent_hit_points - value)
            elif effect_type == "Heal":
                self.opponent_hit_points = min(50, self.opponent_hit_points + value)
    
    def _on_ext_fight_state_update(self, msg: Dict):
        """Handler for ExtFightStateUpdate message"""
        self.current_turn_player_id = msg.get("CurrentTurnPlayerId")
        
        # Determine which state belongs to the player and which to the opponent
        player_state = msg.get("PlayerState", {})
        opponent_state = msg.get("OpponentState", {})
        
        player_state_id = player_state.get("PlayerId")
        opponent_state_id = opponent_state.get("PlayerId")
        
        # Set player and opponent states based on player ID
        if player_state_id == self.player_id:
            self._set_player_state(player_state)
            self._set_opponent_state(opponent_state)
        else:
            self._set_player_state(opponent_state)
            self._set_opponent_state(player_state)
    
    def _set_player_state(self, state: Dict):
        """
        Sets the player's state from a player fight state DTO
        
        Args:
            state: The player state data from the server
        """
        self.player_hit_points = state.get("HitPoints", 50)
        self.player_action_points = state.get("ActionPoints", 0)
        self.player_deck_count = state.get("DeckCount", 0)
        self.player_discard_pile_count = state.get("DiscardPileCount", 0)
        
        # Update player hand
        self.cards_in_hand.clear()
        hand = state.get("Hand", [])
        for card in hand:
            self.cards_in_hand.append(card)
        
        # Update player status effects
        self.player_status_effects.clear()
        status_effects = state.get("StatusEffects", [])
        for effect in status_effects:
            self.player_status_effects.append(effect)
    
    def _set_opponent_state(self, state: Dict):
        """
        Sets the opponent's state from a player fight state DTO
        
        Args:
            state: The opponent state data from the server
        """
        self.opponent_hit_points = state.get("HitPoints", 50)
        self.opponent_action_points = state.get("ActionPoints", 0)
        self.opponent_deck_count = state.get("DeckCount", 0)
        self.opponent_discard_pile_count = state.get("DiscardPileCount", 0)
        
        # Update opponent hand
        self.opponent_cards_in_hand.clear()
        hand = state.get("Hand", [])
        for card in hand:
            self.opponent_cards_in_hand.append(card)
        
        # Update opponent status effects
        self.opponent_status_effects.clear()
        status_effects = state.get("StatusEffects", [])
        for effect in status_effects:
            self.opponent_status_effects.append(effect)
    
    async def close(self):
        """Closes the WebSocket connection and cleans up resources"""
        if not self._disposed and self._websocket:
            await self._websocket.close()
            self._websocket = None
            self._disposed = True
    
    async def __aenter__(self):
        """Async context manager entry"""
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Async context manager exit"""
        await self.close()
    
    # RL-specific methods
    
    def get_observation(self) -> Dict:
        """
        Transform the current game state into an observation for the RL agent.
        
        Returns:
            Dict: Dictionary containing the observation
        """
        if not self.is_in_fight:
            # Return map-level observation if not in fight
            return {
                'is_in_fight': False,
                'player_id': self.player_id,
                'player_position': self.player_position,
                'other_players': {
                    player_id: {
                        'position': info.get('Position'),
                        'in_fight': player_id in self.players_in_fight
                    } for player_id, info in self.other_player_info.items()
                }
            }
        
        # Return card battle observation if in fight
        return {
            'is_in_fight': True,
            'is_player_turn': self.is_player_turn,
            'player_id': self.player_id,
            'opponent_id': self.opponent_id,
            'player_hit_points': self.player_hit_points,
            'player_action_points': self.player_action_points,
            'player_deck_count': self.player_deck_count,
            'player_discard_pile_count': self.player_discard_pile_count,
            'opponent_hit_points': self.opponent_hit_points,
            'opponent_action_points': self.opponent_action_points,
            'opponent_deck_count': self.opponent_deck_count,
            'opponent_discard_pile_count': self.opponent_discard_pile_count,
            'player_cards_in_hand': self.cards_in_hand,
            'opponent_cards_in_hand_count': len(self.opponent_cards_in_hand),
            'player_status_effects': self.player_status_effects,
            'opponent_status_effects': self.opponent_status_effects,
            'last_played_card': self.last_played_card
        }
    
    def get_valid_actions(self) -> Dict:
        """
        Get the valid actions in the current state.
        
        Returns:
            Dict: Dictionary with valid action types and specific actions
        """
        if not self.is_in_fight:
            # Map-level actions
            available_players = [
                player_id for player_id, info in self.other_player_info.items()
                if player_id not in self.players_in_fight
            ]
            
            return {
                'type': 'map',
                'can_challenge': len(available_players) > 0,
                'available_players': available_players
            }
        elif not self.is_player_turn:
            # Opponent's turn, no valid actions
            return {
                'type': 'battle',
                'can_play_card': False,
                'can_end_turn': False,
                'playable_cards': []
            }
        else:
            # Card battle actions during player's turn
            playable_cards = [
                card for card in self.cards_in_hand
                if card.get('Cost', 0) <= self.player_action_points
            ]
            
            return {
                'type': 'battle',
                'can_play_card': len(playable_cards) > 0,
                'can_end_turn': True,
                'playable_cards': playable_cards
            }
    
    def get_reward(self, prev_state: Dict = None) -> float:
        """
        Calculate the reward for the current state transition.
        
        Args:
            prev_state: Previous observation for calculating the state transition reward
            
        Returns:
            float: Reward value
        """
        if not prev_state:
            return 0.0
        
        reward = 0.0
        
        # If fight just ended
        if prev_state.get('is_in_fight', False) and not self.is_in_fight:
            # Check if we won or lost
            winner_id = None
            loser_id = None
            
            # Find the winner and loser in the other_player_info
            for player_id, info in self.other_player_info.items():
                if info.get('FightId') == prev_state.get('current_fight_id'):
                    if player_id == self.player_id:
                        winner_id = self.player_id
                        loser_id = prev_state.get('opponent_id')
                    else:
                        winner_id = prev_state.get('opponent_id')
                        loser_id = self.player_id
            
            # Assign win/loss reward
            if winner_id == self.player_id:
                reward += 1.0  # Major reward for winning
            elif loser_id == self.player_id:
                reward -= 1.0  # Major penalty for losing
        
        # If in fight, calculate intermediate rewards
        elif self.is_in_fight and prev_state.get('is_in_fight', False):
            # Reward for damaging opponent
            prev_opponent_hp = prev_state.get('opponent_hit_points', 50)
            damage_dealt = prev_opponent_hp - self.opponent_hit_points
            if damage_dealt > 0:
                reward += 0.1 * damage_dealt  # Small reward for dealing damage
            
            # Penalty for taking damage
            prev_player_hp = prev_state.get('player_hit_points', 50)
            damage_taken = prev_player_hp - self.player_hit_points
            if damage_taken > 0:
                reward -= 0.1 * damage_taken  # Small penalty for taking damage
            
            # Reward for efficient card play
            if prev_state.get('player_action_points', 0) > self.player_action_points:
                # Played a card (action points decreased)
                if self.last_played_card:
                    card_cost = self.last_played_card.get('Cost', 0)
                    # Simple efficiency measure: damage dealt / cost
                    if card_cost > 0 and damage_dealt > 0:
                        efficiency = damage_dealt / card_cost
                        reward += 0.05 * efficiency  # Small reward for efficient card use
        
        # If just connected to a map and someone isn't in a fight, small reward
        elif not prev_state.get('is_in_fight', False) and self.current_map_id and not self.is_in_fight:
            if any(player_id not in self.players_in_fight for player_id in self.other_player_info):
                reward += 0.01  # Tiny reward for finding a potential opponent
        
        return reward
    
    async def challenge_available_player(self) -> bool:
        """
        Find a player who is not in a fight and challenge them.
        
        Returns:
            bool: True if a challenge was sent successfully, False otherwise
        """
        if self.is_in_fight:
            return False
        
        # Find a player who is not in a fight
        available_players = [
            player_id for player_id in self.other_player_info
            if player_id not in self.players_in_fight
        ]
        
        if not available_players:
            return False
        
        # Challenge the first available player
        target_id = available_players[0]
        challenge_request = {
            "MessageType": "ExtFightChallengeRequest",
            "TargetId": target_id
        }
        
        return await self.send(challenge_request)
    
    async def play_card(self, card_id: str) -> bool:
        """
        Play a card from the player's hand.
        
        Args:
            card_id: ID of the card to play
            
        Returns:
            bool: True if the card play request was sent successfully, False otherwise
        """
        if not self.is_in_fight or not self.is_player_turn:
            return False
        
        # Check if the card is in the player's hand and has a valid cost
        card = next((c for c in self.cards_in_hand if c.get('Id') == card_id), None)
        if not card or card.get('Cost', 0) > self.player_action_points:
            return False
        
        play_card_request = {
            "MessageType": "ExtPlayCardRequest",
            "CardId": card_id
        }
        
        return await self.send(play_card_request)
    
    async def end_turn(self) -> bool:
        """
        End the player's turn.
        
        Returns:
            bool: True if the end turn request was sent successfully, False otherwise
        """
        if not self.is_in_fight or not self.is_player_turn:
            return False
        
        end_turn_request = {
            "MessageType": "ExtEndTurnRequest"
        }
        
        return await self.send(end_turn_request)
    
    async def request_player_id(self) -> bool:
        """
        Send a request to get a player ID from the server.
        
        Returns:
            bool: True if the request was sent successfully, False otherwise
        """
        player_id_request = {
            "MessageType": "ExtPlayerIdRequest"
        }
        
        return await self.send(player_id_request)
    
    async def join_map(self, map_id: str) -> bool:
        """
        Join a specific map.
        
        Args:
            map_id: ID of the map to join
            
        Returns:
            bool: True if the join map request was sent successfully, False otherwise
        """
        join_map_request = {
            "MessageType": "ExtJoinMapRequest",
            "MapId": map_id
        }
        
        return await self.send(join_map_request)
    
    async def request_map_list(self) -> bool:
        """
        Send a request to get the list of available maps.
        
        Returns:
            bool: True if the request was sent successfully, False otherwise
        """
        map_list_request = {
            "MessageType": "ExtMapListRequest"
        }
        
        return await self.send(map_list_request)
