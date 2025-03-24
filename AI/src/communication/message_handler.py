"""
Message handler for serializing and deserializing game messages.
"""
import json
import logging
from collections.abc import Callable, Awaitable
from typing import Dict, Any, Optional, List, Union, Tuple, Type, TypeVar, Generic, cast

from .websocket_client import WebSocketClient


# Type definitions for message classes
T = TypeVar('T', bound='Message')


class Message:
    """Base class for all messages."""
    message_type: str = "MessageBase"
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert message to dictionary for serialization."""
        return {"MessageType": self.message_type}
    
    @classmethod
    def from_dict(cls: Type[T], data: Dict[str, Any]) -> T:
        """Create message instance from dictionary."""
        return cls()


# Connection Messages
class PlayerIdRequest(Message):
    """Client request for connection confirmation."""
    message_type = "PlayerIdRequest"


class PlayerIdResponse(Message):
    """Server response with connection confirmation."""
    message_type = "PlayerIdResponse"
    
    def __init__(self, player_id: str = ""):
        self.player_id = player_id
    
    def to_dict(self) -> Dict[str, Any]:
        data = super().to_dict()
        data["PlayerId"] = self.player_id
        return data
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'PlayerIdResponse':
        return cls(player_id=data.get("PlayerId", ""))


# Map Messages
class MapListRequest(Message):
    """Client request for available maps."""
    message_type = "MapListRequest"


class MapInfo:
    """Information about a map."""
    def __init__(self, id: str = "", name: str = "", width: int = 0, height: int = 0, player_count: int = 0):
        self.id = id
        self.name = name
        self.width = width
        self.height = height
        self.player_count = player_count
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'MapInfo':
        return cls(
            id=data.get("Id", ""),
            name=data.get("Name", ""),
            width=data.get("Width", 0),
            height=data.get("Height", 0),
            player_count=data.get("PlayerCount", 0)
        )


class MapListResponse(Message):
    """Server response with available maps."""
    message_type = "MapListResponse"
    
    def __init__(self, maps: List[MapInfo] = None):
        self.maps = maps or []
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'MapListResponse':
        maps_data = data.get("Maps", [])
        maps = [MapInfo.from_dict(map_data) for map_data in maps_data]
        return cls(maps=maps)


class MapPosition:
    """Position on a map."""
    def __init__(self, x: int = 0, y: int = 0):
        self.x = x
        self.y = y
    
    def to_dict(self) -> Dict[str, Any]:
        return {"X": self.x, "Y": self.y}
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'MapPosition':
        return cls(
            x=data.get("X", 0),
            y=data.get("Y", 0)
        )


class JoinMapRequest(Message):
    """Client request to join a map."""
    message_type = "JoinMapRequest"
    
    def __init__(self, map_id: str = ""):
        self.map_id = map_id
    
    def to_dict(self) -> Dict[str, Any]:
        data = super().to_dict()
        data["MapId"] = self.map_id
        return data


class TilemapData:
    """Tilemap data for a map."""
    def __init__(self, width: int = 0, height: int = 0, tile_data: List[int] = None):
        self.width = width
        self.height = height
        self.tile_data = tile_data or []
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'TilemapData':
        return cls(
            width=data.get("Width", 0),
            height=data.get("Height", 0),
            tile_data=data.get("TileData", [])
        )


class PlayerMapInfo:
    """Information about a player on a map."""
    def __init__(self, position: MapPosition = None, fight_id: Optional[str] = None):
        self.position = position or MapPosition()
        self.fight_id = fight_id
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'PlayerMapInfo':
        position_data = data.get("Position", {})
        position = MapPosition.from_dict(position_data)
        return cls(
            position=position,
            fight_id=data.get("FightId")
        )


class JoinMapCompleted(Message):
    """Server notification that map join has completed successfully."""
    message_type = "JoinMapCompleted"
    
    def __init__(self, map_id: str = "", player_id: str = "", position: MapPosition = None,
                tilemap_data: TilemapData = None, player_info: Dict[str, PlayerMapInfo] = None):
        self.map_id = map_id
        self.player_id = player_id
        self.position = position or MapPosition()
        self.tilemap_data = tilemap_data or TilemapData()
        self.player_info = player_info or {}
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'JoinMapCompleted':
        position_data = data.get("Position", {})
        position = MapPosition.from_dict(position_data)
        
        tilemap_data_dict = data.get("TilemapData", {})
        tilemap_data = TilemapData.from_dict(tilemap_data_dict)
        
        player_info_dict = data.get("PlayerInfo", {})
        player_info = {}
        for player_id, info in player_info_dict.items():
            player_info[player_id] = PlayerMapInfo.from_dict(info)
        
        return cls(
            map_id=data.get("MapId", ""),
            player_id=data.get("PlayerId", ""),
            position=position,
            tilemap_data=tilemap_data,
            player_info=player_info
        )


# Movement Messages
class PlayerMoveRequest(Message):
    """Client request to move player to a new position."""
    message_type = "PlayerMoveRequest"
    
    def __init__(self, new_position: MapPosition = None):
        self.new_position = new_position or MapPosition()
    
    def to_dict(self) -> Dict[str, Any]:
        data = super().to_dict()
        data["NewPosition"] = self.new_position.to_dict()
        return data


class PlayerPositionChange(Message):
    """Server notification of a player position change."""
    message_type = "PlayerPositionChange"
    
    def __init__(self, player_id: str = "", position: Optional[MapPosition] = None):
        self.player_id = player_id
        self.position = position
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'PlayerPositionChange':
        position_data = data.get("Position")
        position = None if position_data is None else MapPosition.from_dict(position_data)
        return cls(
            player_id=data.get("PlayerId", ""),
            position=position
        )


# Fight Messages
class FightChallengeRequest(Message):
    """Client request to send a fight challenge to another player."""
    message_type = "FightChallengeRequest"
    
    def __init__(self, target_id: str = ""):
        self.target_id = target_id
    
    def to_dict(self) -> Dict[str, Any]:
        data = super().to_dict()
        data["TargetId"] = self.target_id
        return data


class FightChallengeReceived(Message):
    """Server notification that a fight challenge was received."""
    message_type = "FightChallengeReceived"
    
    def __init__(self, challenger_id: str = ""):
        self.challenger_id = challenger_id
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'FightChallengeReceived':
        return cls(challenger_id=data.get("ChallengerId", ""))


class FightChallengeAccepted(Message):
    """Client request to accept a fight challenge."""
    message_type = "FightChallengeAccepted"
    
    def __init__(self, target_id: str = ""):
        self.target_id = target_id
    
    def to_dict(self) -> Dict[str, Any]:
        data = super().to_dict()
        data["TargetId"] = self.target_id
        return data


class FightStarted(Message):
    """Server notification that a fight has started."""
    message_type = "FightStarted"
    
    def __init__(self, player1_id: str = "", player2_id: str = ""):
        self.player1_id = player1_id
        self.player2_id = player2_id
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'FightStarted':
        return cls(
            player1_id=data.get("Player1Id", ""),
            player2_id=data.get("Player2Id", "")
        )


class FightEnded(Message):
    """Server notification that a fight has ended."""
    message_type = "FightEnded"
    
    def __init__(self, winner_id: str = "", loser_id: str = "", reason: str = ""):
        self.winner_id = winner_id
        self.loser_id = loser_id
        self.reason = reason
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'FightEnded':
        return cls(
            winner_id=data.get("WinnerId", ""),
            loser_id=data.get("LoserId", ""),
            reason=data.get("Reason", "")
        )


# Card Battle Messages
class CardInfo:
    """Information about a card."""
    def __init__(self, id: str = "", name: str = "", description: str = "", cost: int = 0):
        self.id = id
        self.name = name
        self.description = description
        self.cost = cost
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'CardInfo':
        return cls(
            id=data.get("Id", ""),
            name=data.get("Name", ""),
            description=data.get("Description", ""),
            cost=data.get("Cost", 0)
        )


class StatusEffectInfo:
    """Information about a status effect."""
    def __init__(self, id: str = "", name: str = "", description: str = "", 
                duration: int = 0, type: str = "", magnitude: int = 0):
        self.id = id
        self.name = name
        self.description = description
        self.duration = duration
        self.type = type
        self.magnitude = magnitude
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'StatusEffectInfo':
        return cls(
            id=data.get("Id", ""),
            name=data.get("Name", ""),
            description=data.get("Description", ""),
            duration=data.get("Duration", 0),
            type=data.get("Type", ""),
            magnitude=data.get("Magnitude", 0)
        )


class PlayerFightState:
    """Represents a player's current fight status."""
    def __init__(self, player_id: str = "", hit_points: int = 0, action_points: int = 0,
                hand: List[CardInfo] = None, deck_count: int = 0, discard_pile_count: int = 0,
                status_effects: List[StatusEffectInfo] = None):
        self.player_id = player_id
        self.hit_points = hit_points
        self.action_points = action_points
        self.hand = hand or []
        self.deck_count = deck_count
        self.discard_pile_count = discard_pile_count
        self.status_effects = status_effects or []
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'PlayerFightState':
        hand_data = data.get("Hand", [])
        hand = [CardInfo.from_dict(card_data) for card_data in hand_data]
        
        status_effects_data = data.get("StatusEffects", [])
        status_effects = [StatusEffectInfo.from_dict(effect_data) for effect_data in status_effects_data]
        
        return cls(
            player_id=data.get("PlayerId", ""),
            hit_points=data.get("HitPoints", 0),
            action_points=data.get("ActionPoints", 0),
            hand=hand,
            deck_count=data.get("DeckCount", 0),
            discard_pile_count=data.get("DiscardPileCount", 0),
            status_effects=status_effects
        )


class CardImages(Message):
    """Server notification with SVG data for multiple cards."""
    message_type = "CardImages"
    
    def __init__(self, card_svg_data: Dict[str, str] = None):
        self.card_svg_data = card_svg_data or {}
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'CardImages':
        return cls(card_svg_data=data.get("CardSvgData", {}))


class CardDrawn(Message):
    """Server notification about a newly drawn card."""
    message_type = "CardDrawn"
    
    def __init__(self, card_info: CardInfo = None, svg_data: str = ""):
        self.card_info = card_info or CardInfo()
        self.svg_data = svg_data
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'CardDrawn':
        card_info_data = data.get("CardInfo", {})
        card_info = CardInfo.from_dict(card_info_data)
        return cls(
            card_info=card_info,
            svg_data=data.get("SvgData", "")
        )


class FightStateUpdate(Message):
    """Server notification with complete fight state update."""
    message_type = "FightStateUpdate"
    
    def __init__(self, current_turn_player_id: str = "", 
                player_state: PlayerFightState = None,
                opponent_state: PlayerFightState = None):
        self.current_turn_player_id = current_turn_player_id
        self.player_state = player_state or PlayerFightState()
        self.opponent_state = opponent_state or PlayerFightState()
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'FightStateUpdate':
        player_state_data = data.get("PlayerState", {})
        player_state = PlayerFightState.from_dict(player_state_data)
        
        opponent_state_data = data.get("OpponentState", {})
        opponent_state = PlayerFightState.from_dict(opponent_state_data)
        
        return cls(
            current_turn_player_id=data.get("CurrentTurnPlayerId", ""),
            player_state=player_state,
            opponent_state=opponent_state
        )


class TurnStarted(Message):
    """Server notification that a turn has started."""
    message_type = "TurnStarted"
    
    def __init__(self, active_player_id: str = ""):
        self.active_player_id = active_player_id
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'TurnStarted':
        return cls(active_player_id=data.get("ActivePlayerId", ""))


class TurnEnded(Message):
    """Server notification that a turn has ended."""
    message_type = "TurnEnded"
    
    def __init__(self, player_id: str = ""):
        self.player_id = player_id
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'TurnEnded':
        return cls(player_id=data.get("PlayerId", ""))


class PlayCardRequest(Message):
    """Client request to play a card."""
    message_type = "PlayCardRequest"
    
    def __init__(self, card_id: str = ""):
        self.card_id = card_id
    
    def to_dict(self) -> Dict[str, Any]:
        data = super().to_dict()
        data["CardId"] = self.card_id
        return data


class CardPlayCompleted(Message):
    """Server notification that card play has completed successfully."""
    message_type = "CardPlayCompleted"
    
    def __init__(self, player_id: str = "", played_card: CardInfo = None, 
                effect: str = "", is_visible: bool = True):
        self.player_id = player_id
        self.played_card = played_card or CardInfo()
        self.effect = effect
        self.is_visible = is_visible
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'CardPlayCompleted':
        played_card_data = data.get("PlayedCard", {})
        played_card = CardInfo.from_dict(played_card_data)
        return cls(
            player_id=data.get("PlayerId", ""),
            played_card=played_card,
            effect=data.get("Effect", ""),
            is_visible=data.get("IsVisible", True)
        )


class EffectApplied(Message):
    """Server notification that a card effect was applied."""
    message_type = "EffectApplied"
    
    def __init__(self, target_player_id: str = "", effect_type: str = "", 
                value: int = 0, source: str = ""):
        self.target_player_id = target_player_id
        self.effect_type = effect_type
        self.value = value
        self.source = source
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'EffectApplied':
        return cls(
            target_player_id=data.get("TargetPlayerId", ""),
            effect_type=data.get("EffectType", ""),
            value=data.get("Value", 0),
            source=data.get("Source", "")
        )


class EndTurnRequest(Message):
    """Client request to end the current turn."""
    message_type = "EndTurnRequest"


class GameMessageHandler:
    """
    Handler for game messages.
    """
    
    def __init__(self, websocket_client: WebSocketClient):
        """
        Initialize the message handler.
        
        Args:
            websocket_client: WebSocket client for communication
        """
        self.websocket_client = websocket_client
        self.logger = logging.getLogger("GameMessageHandler")
        self.logger.setLevel(logging.INFO)
        
        # Message type to class mapping
        self.message_classes: Dict[str, Type[Message]] = {
            # Connection messages
            "PlayerIdRequest": PlayerIdRequest,
            "PlayerIdResponse": PlayerIdResponse,
            
            # Map messages
            "MapListRequest": MapListRequest,
            "MapListResponse": MapListResponse,
            "JoinMapRequest": JoinMapRequest,
            "JoinMapCompleted": JoinMapCompleted,
            
            # Movement messages
            "PlayerMoveRequest": PlayerMoveRequest,
            "PlayerPositionChange": PlayerPositionChange,
            
            # Fight messages
            "FightChallengeRequest": FightChallengeRequest,
            "FightChallengeReceived": FightChallengeReceived,
            "FightChallengeAccepted": FightChallengeAccepted,
            "FightStarted": FightStarted,
            "FightEnded": FightEnded,
            
            # Card battle messages
            "CardImages": CardImages,
            "CardDrawn": CardDrawn,
            "FightStateUpdate": FightStateUpdate,
            "TurnStarted": TurnStarted,
            "TurnEnded": TurnEnded,
            "PlayCardRequest": PlayCardRequest,
            "CardPlayCompleted": CardPlayCompleted,
            "EffectApplied": EffectApplied,
            "EndTurnRequest": EndTurnRequest
        }
    
    def send_message(self, message: Message, wait_for_response: bool = False,
                    response_type: Optional[str] = None, timeout: float = 5.0) -> Optional[Message]:
        """
        Send a message to the server.
        
        Args:
            message: Message to send
            wait_for_response: Whether to wait for a response
            response_type: Expected response message type
            timeout: Timeout for waiting for response in seconds
            
        Returns:
            Response message if wait_for_response is True, otherwise None
        """
        message_dict = message.to_dict()
        response_dict = self.websocket_client.send_message(
            message_dict, 
            wait_for_response=wait_for_response,
            response_type=response_type,
            timeout=timeout
        )
        
        if wait_for_response and response_dict is not None:
            return self.parse_message(response_dict)
        
        return None
    
    def parse_message(self, message_dict: Dict[str, Any]) -> Optional[Message]:
        """
        Parse a message dictionary into a message object.
        
        Args:
            message_dict: Message dictionary
            
        Returns:
            Parsed message object or None if message type is unknown
        """
        if "MessageType" not in message_dict:
            self.logger.warning(f"Message has no MessageType: {message_dict}")
            return None
        
        message_type = message_dict["MessageType"]
        if message_type not in self.message_classes:
            self.logger.warning(f"Unknown message type: {message_type}")
            return None
        
        message_class = self.message_classes[message_type]
        return message_class.from_dict(message_dict)
    
    def register_handler(self, message_type: str, handler: Callable[[Message], None]) -> None:
        """
        Register a handler for a specific message type.
        
        Args:
            message_type: Type of message to handle
            handler: Function to call when a message of this type is received
        """
        if message_type not in self.message_classes:
            self.logger.warning(f"Registering handler for unknown message type: {message_type}")
        
        # Create a wrapper that parses the message before calling the handler
        def wrapper(message_dict: Dict[str, Any]) -> None:
            message = self.parse_message(message_dict)
            if message is not None:
                handler(message)
        
        self.websocket_client.register_handler(message_type, wrapper)
    
    def unregister_handler(self, message_type: str) -> None:
        """
        Unregister a handler for a specific message type.
        
        Args:
            message_type: Type of message to unregister handler for
        """
        self.websocket_client.unregister_handler(message_type)
