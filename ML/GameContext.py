import json
import threading
import websocket
from typing import Dict, List, Any, Optional, Callable, TypeVar, Generic, Union

T = TypeVar('T')

class MapPosition:
    """Represents a position on a map."""
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

class PlayerMapInfo:
    """Information about a player on a map."""
    def __init__(self, position: MapPosition, fight_id: Optional[str] = None):
        self.position = position
        self.fight_id = fight_id

class CardInfo:
    """Information about a card."""
    def __init__(self, id: str, name: str, type: str, subtype: str, cost: int, description: str):
        self.id = id
        self.name = name
        self.type = type
        self.subtype = subtype
        self.cost = cost
        self.description = description

class StatusEffectInfo:
    """Information about a status effect."""
    def __init__(self, type: str, value: int, duration: int):
        self.type = type
        self.value = value
        self.duration = duration

class TilemapData:
    """Represents tilemap data for a map."""
    def __init__(self, width: int, height: int, tiles: List[List[int]]):
        self.width = width
        self.height = height
        self.tiles = tiles

class PlayerFightStateDto:
    """Data transfer object for player fight state."""
    def __init__(self, player_id: str, hit_points: int, action_points: int, 
                 deck_count: int, discard_pile_count: int, 
                 hand: List[CardInfo], status_effects: List[StatusEffectInfo]):
        self.player_id = player_id
        self.hit_points = hit_points
        self.action_points = action_points
        self.deck_count = deck_count
        self.discard_pile_count = discard_pile_count
        self.hand = hand
        self.status_effects = status_effects

class GameContext:
    """
    Client-side game state that keeps track of player, map, fight, and card battle state.
    Processes incoming server messages to update the state.
    
    This Python class mirrors the functionality of the C# GameStateContext class.
    """
    
    def __init__(self, server_url: str):
        """
        Initialize a new instance of the GameContext class.
        
        Args:
            server_url: WebSocket URL of the game server
        """
        # Player data
        self.player_id: Optional[str] = None
        self.player_position: Optional[MapPosition] = None
        
        # Map data
        self.current_map_id: Optional[str] = None
        self.current_tilemap_data: Optional[TilemapData] = None
        
        # Other players
        self.other_player_info: Dict[str, PlayerMapInfo] = {}  # Dictionary of player_id -> PlayerMapInfo
        
        # Active fights
        self.active_fights: Dict[str, List[str]] = {}  # Dictionary of fight_id -> list of player_ids
        
        # Fight state
        self.current_fight_id: Optional[str] = None
        self.opponent_id: Optional[str] = None
        
        # Card battle state
        self.card_svg_data: Dict[str, str] = {}  # Dictionary of card_id -> svg data
        self.cards_in_hand: List[CardInfo] = []  # List of CardInfo objects
        self.player_hit_points: int = 50
        self.player_action_points: int = 0
        self.player_deck_count: int = 0
        self.player_discard_pile_count: int = 0
        self.opponent_hit_points: int = 50
        self.opponent_action_points: int = 0
        self.opponent_deck_count: int = 0
        self.opponent_discard_pile_count: int = 0
        self.opponent_cards_in_hand: List[CardInfo] = []  # List of CardInfo objects
        self.player_status_effects: List[StatusEffectInfo] = []  # List of StatusEffectInfo objects
        self.opponent_status_effects: List[StatusEffectInfo] = []  # List of StatusEffectInfo objects
        self.last_played_card: Optional[CardInfo] = None  # CardInfo object
        self.current_turn_player_id: Optional[str] = None
        
        # Pending operations
        self.pending_move: Optional[MapPosition] = None
        
        # WebSocket client setup
        self.server_url = server_url
        self.websocket = None
        self.websocket_thread = None
        
        # Event callbacks
        self.on_server_message_callbacks: List[Callable[[Any], None]] = []

    @property
    def is_in_fight(self) -> bool:
        """Check if the player is currently in a fight."""
        return self.current_fight_id is not None
    
    @property
    def is_moving(self) -> bool:
        """Check if the player is currently moving."""
        return self.pending_move is not None
    
    @property
    def is_player_turn(self) -> bool:
        """Check if it's currently the player's turn in a card battle."""
        return self.current_turn_player_id == self.player_id
    
    async def connect(self):
        """Connect to the WebSocket server."""
        self.websocket = websocket.WebSocketApp(
            self.server_url,
            on_message=self._on_message,
            on_error=self._on_error,
            on_close=self._on_close,
            on_open=self._on_open
        )
        
        # Start the WebSocket connection in a separate thread
        self.websocket_thread = threading.Thread(target=self.websocket.run_forever)
        self.websocket_thread.daemon = True
        self.websocket_thread.start()
    
    def _on_open(self, ws):
        """Handle WebSocket connection opened event."""
        print("WebSocket connection opened")
    
    def _on_message(self, ws, message):
        """Handle incoming WebSocket messages."""
        try:
            # Deserialize the message
            msg_data = json.loads(message)
            ext_server_message = msg_data  # In a real implementation, deserialize to proper object
            
            # Process the message
            self.on_receive(ext_server_message)
            
            # Notify callbacks
            for callback in self.on_server_message_callbacks:
                callback(ext_server_message)
        except Exception as e:
            print(f"Error processing message: {e}")
    
    def _on_error(self, ws, error):
        """Handle WebSocket error event."""
        print(f"WebSocket error: {error}")
    
    def _on_close(self, ws, close_status_code, close_msg):
        """Handle WebSocket connection closed event."""
        print(f"WebSocket connection closed: {close_status_code} - {close_msg}")
    
    def send(self, message: Any) -> bool:
        """
        Send a message to the server.
        
        Args:
            message: The message to send
            
        Returns:
            bool: True if the message was sent successfully, False otherwise
        """
        if self.websocket and self.websocket.sock and self.websocket.sock.connected:
            try:
                serialized_message = json.dumps(message)
                self.websocket.send(serialized_message)
                return True
            except Exception as e:
                print(f"Error sending message: {e}")
                return False
        return False
    
    def add_server_message_callback(self, callback: Callable[[Any], None]):
        """
        Add a callback for server messages.
        
        Args:
            callback: Function to call when a server message is received
        """
        self.on_server_message_callbacks.append(callback)
    
    def on_receive(self, ext_server_message: Dict[str, Any]):
        """
        Process an incoming server message and update the game state.
        
        Args:
            ext_server_message: The server message to process
        """
        # Extract message type
        message_type = ext_server_message.get("Type")
        
        # Map message types to handler methods
        handlers = {
            # Connection messages
            "ExtPlayerIdResponse": self._on_ext_player_id_response,
            
            # Map messages
            "ExtMapListResponse": lambda _: None,  # No state change
            "ExtJoinMapInitiated": self._on_ext_join_map_initiated,
            "ExtJoinMapCompleted": self._on_ext_join_map_completed,
            "ExtJoinMapFailed": lambda _: None,  # Handle join map failure
            "ExtLeaveMapInitiated": self._on_ext_leave_map_initiated,
            "ExtLeaveMapCompleted": self._on_ext_leave_map_completed,
            "ExtLeaveMapFailed": lambda _: None,  # Handle leave map failure
            "ExtPlayerJoinedMap": self._on_ext_player_joined_map,
            "ExtPlayerLeftMap": self._on_ext_player_left_map,
            "ExtPlayerPositionChange": self._on_ext_player_position_change,
            
            # Movement messages
            "ExtMoveInitiated": self._on_ext_move_initiated,
            "ExtMoveCompleted": self._on_ext_move_completed,
            "ExtMoveFailed": self._on_ext_move_failed,
            
            # Fight messages
            "ExtFightStarted": self._on_ext_fight_started,
            "ExtFightEnded": self._on_ext_fight_ended,
            
            # Card battle messages
            "ExtCardImages": self._on_ext_card_images,
            "ExtCardDrawn": self._on_ext_card_drawn,
            "ExtTurnStarted": self._on_ext_turn_started,
            "ExtTurnEnded": self._on_ext_turn_ended,
            "ExtCardPlayInitiated": lambda _: None,  # No state change
            "ExtCardPlayCompleted": self._on_ext_card_play_completed,
            "ExtCardPlayFailed": self._on_ext_card_play_failed,
            "ExtEffectApplied": self._on_ext_effect_applied,
            "ExtFightStateUpdate": self._on_ext_fight_state_update,
        }
        
        # Call the appropriate handler
        handler = handlers.get(message_type)
        if handler:
            handler(ext_server_message)
        else:
            print(f"Message type {message_type} not handled")
    
    def add_player(self, player_id: str, player_info: PlayerMapInfo):
        """
        Add a player to the list of other players.
        
        Args:
            player_id: The ID of the player to add
            player_info: Information about the player
        """
        if player_id != self.player_id:
            # Store the player info
            self.other_player_info[player_id] = player_info
            
            # If the player is in a fight, add them to the active_fights dictionary
            if player_info.fight_id:
                if player_info.fight_id not in self.active_fights:
                    self.active_fights[player_info.fight_id] = []
                self.active_fights[player_info.fight_id].append(player_id)
    
    def update_player_position(self, player_id: str, position: MapPosition):
        """
        Update the position of a player in the list of other players.
        
        Args:
            player_id: The ID of the player to update
            position: The new position of the player
        """
        if player_id != self.player_id and player_id in self.other_player_info:
            # Update the player's position
            old_info = self.other_player_info[player_id]
            self.other_player_info[player_id] = PlayerMapInfo(position, old_info.fight_id)
    
    def remove_player(self, player_id: str):
        """
        Remove a player from the list of other players.
        
        Args:
            player_id: The ID of the player to remove
        """
        self.other_player_info.pop(player_id, None)
    
    #region Connection Message Handlers
    
    def _on_ext_player_id_response(self, msg: Dict[str, Any]):
        """Handle the player ID response message."""
        self.player_id = msg.get("PlayerId")
    
    #endregion
    
    #region Map Message Handlers
    
    def _on_ext_join_map_initiated(self, msg: Dict[str, Any]):
        """Handle the join map initiated message."""
        self.current_map_id = msg.get("MapId")
    
    def _on_ext_join_map_completed(self, msg: Dict[str, Any]):
        """Handle the join map completed message."""
        # Set map data
        self.current_map_id = msg.get("MapId")
        self.current_tilemap_data = msg.get("TilemapData")  # Assuming TilemapData can be deserialized
        
        # Set player position
        self.player_position = msg.get("Position")  # Assuming Position can be deserialized
        
        # Add other players
        self.other_player_info.clear()
        
        player_info = msg.get("PlayerInfo", {})
        for player_id, info in player_info.items():
            if player_id != self.player_id:
                self.add_player(player_id, info)  # Assuming PlayerMapInfo can be deserialized
    
    def _on_ext_leave_map_initiated(self, msg: Dict[str, Any]):
        """Handle the leave map initiated message."""
        # No state changes needed
        pass
    
    def _on_ext_leave_map_completed(self, msg: Dict[str, Any]):
        """Handle the leave map completed message."""
        # Clear map data
        self.current_map_id = None
        self.current_tilemap_data = None
        
        # Clear player position
        self.player_position = None
        
        # Clear other players
        self.other_player_info.clear()
    
    def _on_ext_player_joined_map(self, msg: Dict[str, Any]):
        """Handle the player joined map message."""
        player_id = msg.get("PlayerId")
        position = msg.get("Position")  # Assuming Position can be deserialized
        
        if player_id != self.player_id and position is not None:
            # Create player info
            player_info = PlayerMapInfo(position)
            
            # Add player to collections
            self.add_player(player_id, player_info)
    
    def _on_ext_player_left_map(self, msg: Dict[str, Any]):
        """Handle the player left map message."""
        player_id = msg.get("PlayerId")
        
        if player_id != self.player_id:
            self.remove_player(player_id)
    
    def _on_ext_player_position_change(self, msg: Dict[str, Any]):
        """Handle the player position change message."""
        player_id = msg.get("PlayerId")
        position = msg.get("Position")  # Assuming Position can be deserialized
        
        if player_id == self.player_id and position is not None:
            # Update player position
            self.player_position = position
        elif position is not None:
            # Update other player position
            self.update_player_position(player_id, position)
    
    #endregion
    
    #region Movement Message Handlers
    
    def _on_ext_move_initiated(self, msg: Dict[str, Any]):
        """Handle the move initiated message."""
        self.pending_move = msg.get("NewPosition")  # Assuming NewPosition can be deserialized
    
    def _on_ext_move_completed(self, msg: Dict[str, Any]):
        """Handle the move completed message."""
        self.player_position = msg.get("NewPosition")  # Assuming NewPosition can be deserialized
        self.pending_move = None
    
    def _on_ext_move_failed(self, msg: Dict[str, Any]):
        """Handle the move failed message."""
        self.pending_move = None
    
    #endregion
    
    #region Fight Message Handlers
    
    def _on_ext_fight_started(self, msg: Dict[str, Any]):
        """Handle the fight started message."""
        fight_id = msg.get("FightId")
        player1_id = msg.get("Player1Id")
        player2_id = msg.get("Player2Id")
        
        # Add the fight to active fights
        self.active_fights[fight_id] = [player1_id, player2_id]
        
        # Update the fight ID in other_player_info for both players
        if player1_id in self.other_player_info:
            old_info = self.other_player_info[player1_id]
            self.other_player_info[player1_id] = PlayerMapInfo(old_info.position, fight_id)
        
        if player2_id in self.other_player_info:
            old_info = self.other_player_info[player2_id]
            self.other_player_info[player2_id] = PlayerMapInfo(old_info.position, fight_id)
        
        # Set fight state for the main player if they're involved
        if player1_id == self.player_id or player2_id == self.player_id:
            self.current_fight_id = fight_id
            self.opponent_id = player1_id if player2_id == self.player_id else player2_id
            
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
    
    def _on_ext_fight_ended(self, msg: Dict[str, Any]):
        """Handle the fight ended message."""
        fight_id = msg.get("FightId")
        winner_id = msg.get("WinnerId")
        loser_id = msg.get("LoserId")
        
        # Remove the fight from active fights
        self.active_fights.pop(fight_id, None)
        
        # Clear fight IDs in other_player_info
        if winner_id in self.other_player_info:
            old_info = self.other_player_info[winner_id]
            self.other_player_info[winner_id] = PlayerMapInfo(old_info.position, None)
        
        if loser_id in self.other_player_info:
            old_info = self.other_player_info[loser_id]
            self.other_player_info[loser_id] = PlayerMapInfo(old_info.position, None)
        
        # Reset fight state if the player was involved
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
    
    #endregion
    
    #region Card Battle Message Handlers
    
    def _on_ext_card_images(self, msg: Dict[str, Any]):
        """Handle the card images message."""
        card_svg_data = msg.get("CardSvgData", {})
        for card_id, svg_data in card_svg_data.items():
            self.card_svg_data[card_id] = svg_data
    
    def _on_ext_card_drawn(self, msg: Dict[str, Any]):
        """Handle the card drawn message."""
        # Only update the SVG data cache, don't modify the hand
        card_id = msg.get("CardInfo", {}).get("Id")
        svg_data = msg.get("SvgData")
        
        if card_id and svg_data:
            self.card_svg_data[card_id] = svg_data
        
        # Hand updates are now handled by _on_ext_fight_state_update
    
    def _on_ext_turn_started(self, msg: Dict[str, Any]):
        """Handle the turn started message."""
        self.current_turn_player_id = msg.get("ActivePlayerId")
        
        # Card handling is now managed by _on_ext_fight_state_update
    
    def _on_ext_turn_ended(self, msg: Dict[str, Any]):
        """Handle the turn ended message."""
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
    
    def _on_ext_card_play_completed(self, msg: Dict[str, Any]):
        """Handle the card play completed message."""
        player_id = msg.get("PlayerId")
        played_card = msg.get("PlayedCard")  # Assuming PlayedCard can be deserialized
        
        if played_card:
            card_id = played_card.get("Id")
            
            # Store the last played card
            self.last_played_card = played_card
            
            if player_id == self.player_id:
                # Remove the card from the player's hand
                self.cards_in_hand = [card for card in self.cards_in_hand if card.id != card_id]
            else:
                # Remove the card from the opponent's hand
                self.opponent_cards_in_hand = [card for card in self.opponent_cards_in_hand if card.id != card_id]
    
    def _on_ext_card_play_failed(self, msg: Dict[str, Any]):
        """Handle the card play failed message."""
        # Handle card play failure (log or other handling)
        pass
    
    def _on_ext_effect_applied(self, msg: Dict[str, Any]):
        """Handle the effect applied message."""
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
    
    def _on_ext_fight_state_update(self, msg: Dict[str, Any]):
        """Handle the fight state update message."""
        self.current_turn_player_id = msg.get("CurrentTurnPlayerId")
        
        # Get player and opponent states
        player_state = msg.get("PlayerState", {})
        opponent_state = msg.get("OpponentState", {})
        
        # Determine which state belongs to the player and which to the opponent
        player_state_id = player_state.get("PlayerId")
        opponent_state_id = opponent_state.get("PlayerId")
        
        # Set player and opponent states based on player ID
        if player_state_id == self.player_id:
            self._set_player_state(player_state)
            self._set_opponent_state(opponent_state)
        else:
            self._set_player_state(opponent_state)
            self._set_opponent_state(player_state)
    
    def _set_player_state(self, state: Dict[str, Any]):
        """Set the player's state in a card battle."""
        self.player_hit_points = state.get("HitPoints", 50)
        self.player_action_points = state.get("ActionPoints", 0)
        self.player_deck_count = state.get("DeckCount", 0)
        self.player_discard_pile_count = state.get("DiscardPileCount", 0)
        
        # Update player hand
        self.cards_in_hand = state.get("Hand", [])  # Assuming CardInfo objects can be deserialized
        
        # Update player status effects
        self.player_status_effects = state.get("StatusEffects", [])  # Assuming StatusEffectInfo objects can be deserialized
    
    def _set_opponent_state(self, state: Dict[str, Any]):
        """Set the opponent's state in a card battle."""
        self.opponent_hit_points = state.get("HitPoints", 50)
        self.opponent_action_points = state.get("ActionPoints", 0)
        self.opponent_deck_count = state.get("DeckCount", 0)
        self.opponent_discard_pile_count = state.get("DiscardPileCount", 0)
        
        # Update opponent hand
        self.opponent_cards_in_hand = state.get("Hand", [])  # Assuming CardInfo objects can be deserialized
        
        # Update opponent status effects
        self.opponent_status_effects = state.get("StatusEffects", [])  # Assuming StatusEffectInfo objects can be deserialized
    
    #endregion
    
    def dispose(self):
        """Dispose of resources."""
        if self.websocket:
            self.websocket.close()
            self.websocket = None
        
        if self.websocket_thread and self.websocket_thread.is_alive():
            self.websocket_thread.join(timeout=1.0)
            self.websocket_thread = None
