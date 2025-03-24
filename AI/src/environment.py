"""
Reinforcement learning environment wrapper for the card battle game.
"""
import logging
import time
import random
import numpy as np
from typing import Dict, Any, Optional, List, Tuple, Union, Set

import gymnasium as gym
from gymnasium import spaces

from .communication.websocket_client import WebSocketClient
from .communication.message_handler import (
    GameMessageHandler, Message, PlayerIdRequest, PlayerIdResponse,
    MapListRequest, MapListResponse, JoinMapRequest, JoinMapCompleted,
    PlayerMoveRequest, PlayerPositionChange, FightChallengeRequest,
    FightChallengeReceived, FightChallengeAccepted, FightStarted, FightEnded,
    CardImages, CardDrawn, FightStateUpdate, TurnStarted, TurnEnded,
    PlayCardRequest, CardPlayCompleted, EffectApplied, EndTurnRequest,
    CardInfo, StatusEffectInfo, PlayerFightState, MapPosition, MapInfo
)
from .utils.config import Config


class CardBattleEnv(gym.Env):
    """
    Reinforcement learning environment for the card battle game.
    """
    
    metadata = {"render_modes": ["human", "rgb_array"], "render_fps": 4}
    
    def __init__(self, config: Config, agent_id: str = ""):
        """
        Initialize the environment.
        
        Args:
            config: Configuration
            agent_id: Unique identifier for this agent
        """
        super().__init__()
        
        self.config = config
        self.agent_id = agent_id or f"agent_{random.randint(0, 10000)}"
        
        # Set up logging
        self.logger = logging.getLogger(f"CardBattleEnv-{self.agent_id}")
        self.logger.setLevel(logging.INFO)
        
        # Communication
        self.websocket_client = WebSocketClient(config.server)
        self.message_handler = GameMessageHandler(self.websocket_client)
        
        # Game state
        self.player_id: Optional[str] = None
        self.current_map_id: Optional[str] = None
        self.available_maps: List[MapInfo] = []
        self.player_position: Optional[MapPosition] = None
        self.players_on_map: Dict[str, MapPosition] = {}
        self.in_fight: bool = False
        self.opponent_id: Optional[str] = None
        self.is_my_turn: bool = False
        self.fight_state: Optional[FightStateUpdate] = None
        self.card_images: Dict[str, str] = {}
        self.drawn_cards: List[CardInfo] = []
        
        # Environment state
        self.done: bool = False
        self.reward: float = 0.0
        self.info: Dict[str, Any] = {}
        
        # Define action and observation spaces
        # Action space: Which card to play (0 to max_hand_size-1) or end turn (max_hand_size)
        self.max_hand_size = 10
        self.action_space = spaces.Discrete(self.max_hand_size + 1)
        
        # Observation space: Complex state representation
        # We'll use a Dict space with multiple components
        self.observation_space = spaces.Dict({
            # Player state
            "player_hp": spaces.Box(low=0, high=100, shape=(1,), dtype=np.float32),
            "player_ap": spaces.Box(low=0, high=20, shape=(1,), dtype=np.float32),
            "player_deck_count": spaces.Box(low=0, high=30, shape=(1,), dtype=np.float32),
            "player_discard_count": spaces.Box(low=0, high=30, shape=(1,), dtype=np.float32),
            
            # Opponent state
            "opponent_hp": spaces.Box(low=0, high=100, shape=(1,), dtype=np.float32),
            "opponent_ap": spaces.Box(low=0, high=20, shape=(1,), dtype=np.float32),
            "opponent_hand_count": spaces.Box(low=0, high=self.max_hand_size, shape=(1,), dtype=np.float32),
            "opponent_deck_count": spaces.Box(low=0, high=30, shape=(1,), dtype=np.float32),
            "opponent_discard_count": spaces.Box(low=0, high=30, shape=(1,), dtype=np.float32),
            
            # Cards in hand (one-hot encoded by card type and cost)
            # We'll use a fixed size array and pad with zeros
            "hand": spaces.Box(
                low=0, 
                high=1, 
                shape=(self.max_hand_size, config.model.state_dim), 
                dtype=np.float32
            ),
            
            # Status effects (player and opponent)
            "player_status_effects": spaces.Box(
                low=0, 
                high=10, 
                shape=(10, 3),  # 10 possible effects, each with type, magnitude, duration
                dtype=np.float32
            ),
            "opponent_status_effects": spaces.Box(
                low=0, 
                high=10, 
                shape=(10, 3),  # 10 possible effects, each with type, magnitude, duration
                dtype=np.float32
            ),
            
            # Turn information
            "is_my_turn": spaces.Box(low=0, high=1, shape=(1,), dtype=np.float32),
        })
        
        # Message handlers
        self._setup_message_handlers()
        
        # Connect to server
        self.connected = False
    
    def _setup_message_handlers(self) -> None:
        """Set up handlers for different message types."""
        self.message_handler.register_handler("ExtPlayerIdResponse", self._handle_player_id_response)
        self.message_handler.register_handler("ExtMapListResponse", self._handle_map_list_response)
        self.message_handler.register_handler("ExtJoinMapCompleted", self._handle_join_map_completed)
        self.message_handler.register_handler("ExtPlayerPositionChange", self._handle_player_position_change)
        self.message_handler.register_handler("ExtFightChallengeReceived", self._handle_fight_challenge_received)
        self.message_handler.register_handler("ExtFightStarted", self._handle_fight_started)
        self.message_handler.register_handler("ExtFightEnded", self._handle_fight_ended)
        self.message_handler.register_handler("ExtCardImages", self._handle_card_images)
        self.message_handler.register_handler("ExtCardDrawn", self._handle_card_drawn)
        self.message_handler.register_handler("ExtFightStateUpdate", self._handle_fight_state_update)
        self.message_handler.register_handler("ExtTurnStarted", self._handle_turn_started)
        self.message_handler.register_handler("ExtTurnEnded", self._handle_turn_ended)
        self.message_handler.register_handler("ExtCardPlayCompleted", self._handle_card_play_completed)
        self.message_handler.register_handler("ExtEffectApplied", self._handle_effect_applied)
    
    def connect(self) -> bool:
        """
        Connect to the game server.
        
        Returns:
            True if connection was successful, False otherwise
        """
        if self.connected:
            return True
        
        # Connect WebSocket
        if not self.websocket_client.connect():
            self.logger.error("Failed to connect to WebSocket server")
            return False
        
        # Request player ID
        response = self.message_handler.send_message(
            PlayerIdRequest(),
            wait_for_response=True,
            response_type="ExtPlayerIdResponse",
            timeout=5.0
        )
        
        if response is None or not isinstance(response, PlayerIdResponse):
            self.logger.error("Failed to get player ID")
            return False
        
        self.player_id = response.player_id
        self.logger.info(f"Connected with player ID: {self.player_id}")
        self.connected = True
        
        return True
    
    def disconnect(self) -> None:
        """Disconnect from the game server."""
        if not self.connected:
            return
        
        self.websocket_client.disconnect()
        self.connected = False
        self.logger.info("Disconnected from server")
    
    def get_available_maps(self) -> List[MapInfo]:
        """
        Get the list of available maps.
        
        Returns:
            List of available maps
        """
        if not self.connected:
            self.logger.error("Not connected to server")
            return []
        
        response = self.message_handler.send_message(
            MapListRequest(),
            wait_for_response=True,
            response_type="ExtMapListResponse",
            timeout=5.0
        )
        
        if response is None or not isinstance(response, MapListResponse):
            self.logger.error("Failed to get map list")
            return []
        
        self.available_maps = response.maps
        return self.available_maps
    
    def join_map(self, map_id: Optional[str] = None) -> bool:
        """
        Join a map.
        
        Args:
            map_id: ID of the map to join, or None to join the first available map
            
        Returns:
            True if join was successful, False otherwise
        """
        if not self.connected:
            self.logger.error("Not connected to server")
            return False
        
        # Get available maps if needed
        if not self.available_maps:
            self.get_available_maps()
            if not self.available_maps:
                self.logger.error("No maps available")
                return False
        
        # Select map if not specified
        if map_id is None:
            map_id = self.available_maps[0].id
        
        # Join map
        response = self.message_handler.send_message(
            JoinMapRequest(map_id=map_id),
            wait_for_response=True,
            response_type="ExtJoinMapCompleted",
            timeout=5.0
        )
        
        if response is None or not isinstance(response, JoinMapCompleted):
            self.logger.error(f"Failed to join map {map_id}")
            return False
        
        self.current_map_id = map_id
        self.player_position = response.position
        
        # Update players on map
        self.players_on_map = {}
        for player_id, info in response.player_info.items():
            if player_id != self.player_id:
                self.players_on_map[player_id] = info.position
        
        self.logger.info(f"Joined map {map_id} at position {self.player_position.x}, {self.player_position.y}")
        return True
    
    def find_player_not_in_fight(self) -> Optional[str]:
        """
        Find a player on the map who is not in a fight.
        
        Returns:
            Player ID if found, None otherwise
        """
        if not self.connected or not self.current_map_id:
            self.logger.error("Not connected to server or not on a map")
            return None
        
        # Check if there are any players on the map
        if not self.players_on_map:
            self.logger.warning("No other players on the map")
            return None
        
        # Find a player who is not in a fight
        for player_id, position in self.players_on_map.items():
            # TODO: Check if player is in a fight
            # For now, just return the first player
            return player_id
        
        self.logger.warning("All players are in fights")
        return None
    
    def challenge_player(self, target_id: str) -> bool:
        """
        Challenge a player to a fight.
        
        Args:
            target_id: ID of the player to challenge
            
        Returns:
            True if challenge was successful, False otherwise
        """
        if not self.connected or not self.current_map_id:
            self.logger.error("Not connected to server or not on a map")
            return False
        
        # Send challenge
        self.message_handler.send_message(
            FightChallengeRequest(target_id=target_id),
            wait_for_response=False
        )
        
        # Wait for fight to start
        timeout = 10.0  # seconds
        start_time = time.time()
        while not self.in_fight and time.time() - start_time < timeout:
            time.sleep(0.1)
        
        if not self.in_fight:
            self.logger.error(f"Failed to start fight with {target_id}")
            return False
        
        self.logger.info(f"Started fight with {target_id}")
        return True
    
    def play_card(self, card_index: int) -> bool:
        """
        Play a card from the hand.
        
        Args:
            card_index: Index of the card in the hand to play
            
        Returns:
            True if card play was successful, False otherwise
        """
        if not self.connected or not self.in_fight:
            self.logger.error("Not connected to server or not in a fight")
            return False
        
        if not self.is_my_turn:
            self.logger.error("Not my turn")
            return False
        
        if self.fight_state is None:
            self.logger.error("Fight state is None")
            return False
        
        # Check if card index is valid
        if card_index < 0 or card_index >= len(self.fight_state.player_state.hand):
            self.logger.error(f"Invalid card index: {card_index}")
            return False
        
        # Get card ID
        card_id = self.fight_state.player_state.hand[card_index].id
        
        # Send play card request
        self.message_handler.send_message(
            PlayCardRequest(card_id=card_id),
            wait_for_response=False
        )
        
        # Wait for card play to complete
        # In a real implementation, we would wait for the CardPlayCompleted message
        # For now, just return True
        return True
    
    def end_turn(self) -> bool:
        """
        End the current turn.
        
        Returns:
            True if end turn was successful, False otherwise
        """
        if not self.connected or not self.in_fight:
            self.logger.error("Not connected to server or not in a fight")
            return False
        
        if not self.is_my_turn:
            self.logger.error("Not my turn")
            return False
        
        # Send end turn request
        self.message_handler.send_message(
            EndTurnRequest(),
            wait_for_response=False
        )
        
        # Wait for turn to end
        timeout = 5.0  # seconds
        start_time = time.time()
        while self.is_my_turn and time.time() - start_time < timeout:
            time.sleep(0.1)
        
        if self.is_my_turn:
            self.logger.error("Failed to end turn")
            return False
        
        self.logger.info("Ended turn")
        return True
    
    def reset(self, seed: Optional[int] = None, options: Optional[Dict[str, Any]] = None) -> Tuple[Dict[str, np.ndarray], Dict[str, Any]]:
        """
        Reset the environment.
        
        Args:
            seed: Random seed
            options: Additional options
            
        Returns:
            Initial observation and info
        """
        super().reset(seed=seed)
        
        # Connect to server if not connected
        if not self.connected:
            if not self.connect():
                raise RuntimeError("Failed to connect to server")
        
        # Join a map if not on one
        if not self.current_map_id:
            if not self.join_map():
                raise RuntimeError("Failed to join a map")
        
        # Reset state
        self.in_fight = False
        self.opponent_id = None
        self.is_my_turn = False
        self.fight_state = None
        self.card_images = {}
        self.drawn_cards = []
        self.done = False
        self.reward = 0.0
        self.info = {}
        
        # Find a player to challenge
        target_id = self.find_player_not_in_fight()
        if target_id is None:
            self.logger.warning("No players to challenge, waiting...")
            # In a real implementation, we would wait for a player to become available
            # For now, just return a dummy observation
            return self._get_observation(), self.info
        
        # Challenge the player
        if not self.challenge_player(target_id):
            self.logger.warning("Failed to challenge player, returning dummy observation")
            return self._get_observation(), self.info
        
        # Wait for fight to initialize
        timeout = 10.0  # seconds
        start_time = time.time()
        while self.fight_state is None and time.time() - start_time < timeout:
            time.sleep(0.1)
        
        if self.fight_state is None:
            self.logger.warning("Fight state not initialized, returning dummy observation")
            return self._get_observation(), self.info
        
        return self._get_observation(), self.info
    
    def step(self, action: int) -> Tuple[Dict[str, np.ndarray], float, bool, bool, Dict[str, Any]]:
        """
        Take a step in the environment.
        
        Args:
            action: Action to take (card index to play or end turn)
            
        Returns:
            Observation, reward, terminated, truncated, info
        """
        if not self.connected or not self.in_fight:
            self.logger.error("Not connected to server or not in a fight")
            return self._get_observation(), 0.0, True, False, {"error": "Not in a fight"}
        
        if self.done:
            self.logger.warning("Episode is already done")
            return self._get_observation(), 0.0, True, False, {"error": "Episode is done"}
        
        if not self.is_my_turn:
            self.logger.error("Not my turn")
            return self._get_observation(), 0.0, False, False, {"error": "Not my turn"}
        
        # Process action
        if action < self.max_hand_size:
            # Play a card
            if self.fight_state is None or action >= len(self.fight_state.player_state.hand):
                self.logger.error(f"Invalid card index: {action}")
                return self._get_observation(), -0.1, False, False, {"error": "Invalid card index"}
            
            # Record state before action
            prev_player_hp = self.fight_state.player_state.hit_points
            prev_opponent_hp = self.fight_state.opponent_state.hit_points
            
            # Play the card
            if not self.play_card(action):
                self.logger.error("Failed to play card")
                return self._get_observation(), -0.1, False, False, {"error": "Failed to play card"}
            
            # Wait for state update
            timeout = 5.0  # seconds
            start_time = time.time()
            while self.fight_state is not None and time.time() - start_time < timeout:
                # Check if fight ended
                if self.done:
                    break
                
                # Check if state was updated
                if (self.fight_state.player_state.hit_points != prev_player_hp or
                    self.fight_state.opponent_state.hit_points != prev_opponent_hp):
                    break
                
                time.sleep(0.1)
            
            # Calculate reward
            reward = self._calculate_reward(prev_player_hp, prev_opponent_hp)
            
        else:
            # End turn
            if not self.end_turn():
                self.logger.error("Failed to end turn")
                return self._get_observation(), -0.1, False, False, {"error": "Failed to end turn"}
            
            # Small negative reward for ending turn
            reward = -0.05
        
        # Check if fight ended
        if self.done:
            # Additional reward for winning/losing
            if self.info.get("won", False):
                reward += 1.0
            else:
                reward -= 1.0
        
        return self._get_observation(), reward, self.done, False, self.info
    
    def _calculate_reward(self, prev_player_hp: int, prev_opponent_hp: int) -> float:
        """
        Calculate reward based on state change.
        
        Args:
            prev_player_hp: Player HP before action
            prev_opponent_hp: Opponent HP before action
            
        Returns:
            Reward value
        """
        if self.fight_state is None:
            return 0.0
        
        reward = 0.0
        
        # Reward for damaging opponent
        hp_diff = prev_opponent_hp - self.fight_state.opponent_state.hit_points
        if hp_diff > 0:
            reward += 0.1 * hp_diff
        
        # Penalty for taking damage
        hp_diff = prev_player_hp - self.fight_state.player_state.hit_points
        if hp_diff > 0:
            reward -= 0.1 * hp_diff
        
        # Reward for healing
        hp_diff = self.fight_state.player_state.hit_points - prev_player_hp
        if hp_diff > 0:
            reward += 0.05 * hp_diff
        
        return reward
    
    def _get_observation(self) -> Dict[str, np.ndarray]:
        """
        Get the current observation.
        
        Returns:
            Observation dictionary
        """
        # Default values
        player_hp = np.array([50.0], dtype=np.float32)
        player_ap = np.array([3.0], dtype=np.float32)
        player_deck_count = np.array([25.0], dtype=np.float32)
        player_discard_count = np.array([0.0], dtype=np.float32)
        
        opponent_hp = np.array([50.0], dtype=np.float32)
        opponent_ap = np.array([3.0], dtype=np.float32)
        opponent_hand_count = np.array([5.0], dtype=np.float32)
        opponent_deck_count = np.array([25.0], dtype=np.float32)
        opponent_discard_count = np.array([0.0], dtype=np.float32)
        
        hand = np.zeros((self.max_hand_size, self.config.model.state_dim), dtype=np.float32)
        
        player_status_effects = np.zeros((10, 3), dtype=np.float32)
        opponent_status_effects = np.zeros((10, 3), dtype=np.float32)
        
        is_my_turn = np.array([0.0], dtype=np.float32)
        
        # Update with actual values if in a fight
        if self.fight_state is not None:
            player_state = self.fight_state.player_state
            opponent_state = self.fight_state.opponent_state
            
            player_hp = np.array([float(player_state.hit_points)], dtype=np.float32)
            player_ap = np.array([float(player_state.action_points)], dtype=np.float32)
            player_deck_count = np.array([float(player_state.deck_count)], dtype=np.float32)
            player_discard_count = np.array([float(player_state.discard_pile_count)], dtype=np.float32)
            
            opponent_hp = np.array([float(opponent_state.hit_points)], dtype=np.float32)
            opponent_ap = np.array([float(opponent_state.action_points)], dtype=np.float32)
            opponent_hand_count = np.array([float(len(opponent_state.hand))], dtype=np.float32)
            opponent_deck_count = np.array([float(opponent_state.deck_count)], dtype=np.float32)
            opponent_discard_count = np.array([float(opponent_state.discard_pile_count)], dtype=np.float32)
            
            # Encode cards in hand
            for i, card in enumerate(player_state.hand):
                if i < self.max_hand_size:
                    # Simple encoding: one-hot for card type and cost
                    # In a real implementation, we would use a more sophisticated encoding
                    card_type = 0  # Default
                    if "attack" in card.id.lower():
                        card_type = 1
                    elif "defense" in card.id.lower():
                        card_type = 2
                    elif "utility" in card.id.lower():
                        card_type = 3
                    elif "special" in card.id.lower():
                        card_type = 4
                    
                    # One-hot encode card type
                    hand[i, card_type] = 1.0
                    
                    # Encode cost
                    hand[i, 5] = float(card.cost) / 10.0  # Normalize
            
            # Encode status effects
            for i, effect in enumerate(player_state.status_effects):
                if i < 10:
                    # Encode effect type, magnitude, and duration
                    effect_type = 0  # Default
                    if "damage" in effect.type.lower():
                        effect_type = 1
                    elif "heal" in effect.type.lower():
                        effect_type = 2
                    elif "shield" in effect.type.lower():
                        effect_type = 3
                    
                    player_status_effects[i, 0] = float(effect_type)
                    player_status_effects[i, 1] = float(effect.magnitude) / 10.0  # Normalize
                    player_status_effects[i, 2] = float(effect.duration) / 5.0  # Normalize
            
            for i, effect in enumerate(opponent_state.status_effects):
                if i < 10:
                    # Encode effect type, magnitude, and duration
                    effect_type = 0  # Default
                    if "damage" in effect.type.lower():
                        effect_type = 1
                    elif "heal" in effect.type.lower():
                        effect_type = 2
                    elif "shield" in effect.type.lower():
                        effect_type = 3
                    
                    opponent_status_effects[i, 0] = float(effect_type)
                    opponent_status_effects[i, 1] = float(effect.magnitude) / 10.0  # Normalize
                    opponent_status_effects[i, 2] = float(effect.duration) / 5.0  # Normalize
            
            # Turn information
            is_my_turn = np.array([1.0 if self.is_my_turn else 0.0], dtype=np.float32)
        
        return {
            "player_hp": player_hp,
            "player_ap": player_ap,
            "player_deck_count": player_deck_count,
            "player_discard_count": player_discard_count,
            
            "opponent_hp": opponent_hp,
            "opponent_ap": opponent_ap,
            "opponent_hand_count": opponent_hand_count,
            "opponent_deck_count": opponent_deck_count,
            "opponent_discard_count": opponent_discard_count,
            
            "hand": hand,
            
            "player_status_effects": player_status_effects,
            "opponent_status_effects": opponent_status_effects,
            
            "is_my_turn": is_my_turn,
        }
    
    def render(self, mode: str = "human") -> Optional[np.ndarray]:
        """
        Render the environment.
        
        Args:
            mode: Rendering mode
            
        Returns:
            Rendered frame if mode is "rgb_array", otherwise None
        """
        if mode == "human":
            # Print current state
            if self.fight_state is not None:
                player_state = self.fight_state.player_state
                opponent_state = self.fight_state.opponent_state
                
                print(f"Player: HP={player_state.hit_points}, AP={player_state.action_points}")
                print(f"Opponent: HP={opponent_state.hit_points}, AP={opponent_state.action_points}")
                print(f"Turn: {'Player' if self.is_my_turn else 'Opponent'}")
                
                print("Hand:")
                for i, card in enumerate(player_state.hand):
                    print(f"  {i}: {card.name} (Cost: {card.cost}) - {card.description}")
            else:
                print("Not in a fight")
            
            return None
        
        elif mode == "rgb_array":
            # Return a simple visualization
            # In a real implementation, we would render a proper image
            return np.zeros((400, 600, 3), dtype=np.uint8)
        
        else:
            raise ValueError(f"Unsupported render mode: {mode}")
    
    def close(self) -> None:
        """Close the environment."""
        self.disconnect()
    
    # Message handlers
    
    def _handle_player_id_response(self, message: Message) -> None:
        """Handle PlayerIdResponse message."""
        if not isinstance(message, PlayerIdResponse):
            return
        
        self.player_id = message.player_id
        self.logger.info(f"Received player ID: {self.player_id}")
    
    def _handle_map_list_response(self, message: Message) -> None:
        """Handle MapListResponse message."""
        if not isinstance(message, MapListResponse):
            return
        
        self.available_maps = message.maps
        self.logger.info(f"Received {len(self.available_maps)} maps")
    
    def _handle_join_map_completed(self, message: Message) -> None:
        """Handle JoinMapCompleted message."""
        if not isinstance(message, JoinMapCompleted):
            return
        
        self.current_map_id = message.map_id
        self.player_position = message.position
        
        # Update players on map
        self.players_on_map = {}
        for player_id, info in message.player_info.items():
            if player_id != self.player_id:
                self.players_on_map[player_id] = info.position
        
        self.logger.info(f"Joined map {self.current_map_id} at position {self.player_position.x}, {self.player_position.y}")
        self.logger.info(f"There are {len(self.players_on_map)} other players on the map")
    
    def _handle_player_position_change(self, message: Message) -> None:
        """Handle PlayerPositionChange message."""
        if not isinstance(message, PlayerPositionChange):
            return
        
        if message.player_id == self.player_id:
            if message.position is not None:
                self.player_position = message.position
                self.logger.info(f"Player position changed to {self.player_position.x}, {self.player_position.y}")
        else:
            if message.position is not None:
                self.players_on_map[message.player_id] = message.position
                self.logger.info(f"Player {message.player_id} moved to {message.position.x}, {message.position.y}")
            else:
                if message.player_id in self.players_on_map:
                    del self.players_on_map[message.player_id]
                    self.logger.info(f"Player {message.player_id} left the map")
    
    def _handle_fight_challenge_received(self, message: Message) -> None:
        """Handle FightChallengeReceived message."""
        if not isinstance(message, FightChallengeReceived):
            return
        
        # Auto-accept challenges
        self.message_handler.send_message(
            FightChallengeAccepted(target_id=message.challenger_id),
            wait_for_response=False
        )
        
        self.logger.info(f"Received fight challenge from {message.challenger_id}, auto-accepting")
    
    def _handle_fight_started(self, message: Message) -> None:
        """Handle FightStarted message."""
        if not isinstance(message, FightStarted):
            return
        
        self.in_fight = True
        
        # Determine opponent ID
        if message.player1_id == self.player_id:
            self.opponent_id = message.player2_id
        else:
            self.opponent_id = message.player1_id
        
        self.logger.info(f"Fight started with {self.opponent_id}")
    
    def _handle_fight_ended(self, message: Message) -> None:
        """Handle FightEnded message."""
        if not isinstance(message, FightEnded):
            return
        
        self.in_fight = False
        self.done = True
        
        # Determine if we won
        if message.winner_id == self.player_id:
            self.info["won"] = True
            self.logger.info(f"Fight ended, we won! Reason: {message.reason}")
        else:
            self.info["won"] = False
            self.logger.info(f"Fight ended, we lost. Reason: {message.reason}")
    
    def _handle_card_images(self, message: Message) -> None:
        """Handle CardImages message."""
        if not isinstance(message, CardImages):
            return
        
        self.card_images.update(message.card_svg_data)
        self.logger.info(f"Received {len(message.card_svg_data)} card images")
    
    def _handle_card_drawn(self, message: Message) -> None:
        """Handle CardDrawn message."""
        if not isinstance(message, CardDrawn):
            return
        
        self.drawn_cards.append(message.card_info)
        self.logger.info(f"Drew card: {message.card_info.name}")
    
    def _handle_fight_state_update(self, message: Message) -> None:
        """Handle FightStateUpdate message."""
        if not isinstance(message, FightStateUpdate):
            return
        
        self.fight_state = message
        self.logger.debug("Received fight state update")
    
    def _handle_turn_started(self, message: Message) -> None:
        """Handle TurnStarted message."""
        if not isinstance(message, TurnStarted):
            return
        
        self.is_my_turn = message.active_player_id == self.player_id
        
        if self.is_my_turn:
            self.logger.info("Our turn started")
        else:
            self.logger.info("Opponent's turn started")
    
    def _handle_turn_ended(self, message: Message) -> None:
        """Handle TurnEnded message."""
        if not isinstance(message, TurnEnded):
            return
        
        if message.player_id == self.player_id:
            self.is_my_turn = False
            self.logger.info("Our turn ended")
        else:
            self.logger.info("Opponent's turn ended")
    
    def _handle_card_play_completed(self, message: Message) -> None:
        """Handle CardPlayCompleted message."""
        if not isinstance(message, CardPlayCompleted):
            return
        
        if message.player_id == self.player_id:
            self.logger.info(f"We played card: {message.played_card.name}")
        else:
            self.logger.info(f"Opponent played card: {message.played_card.name}")
    
    def _handle_effect_applied(self, message: Message) -> None:
        """Handle EffectApplied message."""
        if not isinstance(message, EffectApplied):
            return
        
        if message.target_player_id == self.player_id:
            self.logger.info(f"Effect applied to us: {message.effect_type} for {message.value}")
        else:
            self.logger.info(f"Effect applied to opponent: {message.effect_type} for {message.value}")
