//using GameServer.Shared.Models;
//using System.Collections.Generic;

//namespace GameServer.Shared.ExternalMessages
//{
//    /// <summary>
//    /// Base class for all external messages with a Type property that contains the derived class name
//    /// </summary>
//    public abstract class BaseExternalMessage
//    {
//        /// <summary>
//        /// The type name of the derived message class
//        /// </summary>
//        public string MessageType { get; }

//        /// <summary>
//        /// Base constructor that initializes the Type property with the derived class name
//        /// </summary>
//        protected BaseExternalMessage()
//        {
//            MessageType = GetType().Name;
//        }
//    }

//    /// <summary>
//    /// Base class for all messages sent from server to client
//    /// </summary>
//    public abstract class ToClientMessage : BaseExternalMessage
//    {
//        protected ToClientMessage() : base() { }
//    }

//    /// <summary>
//    /// Base class for all messages sent from client to server
//    /// </summary>
//    public abstract class FromClientMessage : BaseExternalMessage
//    {
//        protected FromClientMessage() : base() { }
//    }

//    // Connection messages
//    /// <summary>
//    /// Client request for connection confirmation
//    /// </summary>
//    public class InPlayerIdRequest : FromClientMessage
//    {
//        public InPlayerIdRequest() : base() { }
//    }

//    /// <summary>
//    /// Server response with connection confirmation
//    /// </summary>
//    public class OutPlayerIdResponse : ToClientMessage
//    {
//        public string PlayerId { get; }

//        public OutPlayerIdResponse(string playerId) : base()
//        {
//            PlayerId = playerId;
//        }
//    }

//    // Map listing messages
//    /// <summary>
//    /// Client request for available maps
//    /// </summary>
//    public class InRequestMapList : FromClientMessage
//    {
//        public InRequestMapList() : base() { }
//    }

//    /// <summary>
//    /// Server response with available maps
//    /// </summary>
//    public class OutRequestMapListResponse : ToClientMessage
//    {
//        public List<MapInfo> Maps { get; }

//        public OutRequestMapListResponse(List<MapInfo> maps) : base()
//        {
//            Maps = maps;
//        }
//    }

//    /// <summary>
//    /// Information about a map
//    /// </summary>
//    public class MapInfo
//    {
//        public string Id { get; }
//        public string Name { get; }
//        public int Width { get; }
//        public int Height { get; }
//        public int PlayerCount { get; }

//        public MapInfo(string id, string name, int width, int height, int playerCount)
//        {
//            Id = id;
//            Name = name;
//            Width = width;
//            Height = height;
//            PlayerCount = playerCount;
//        }
//    }

//    // Map join/leave messages
//    /// <summary>
//    /// Client request to join a map
//    /// </summary>
//    public class InJoinMap : FromClientMessage
//    {
//        public string MapId { get; }

//        public InJoinMap(string mapId) : base()
//        {
//            MapId = mapId;
//        }
//    }

//    /// <summary>
//    /// Client request to leave a map
//    /// </summary>
//    public class InLeaveMap : FromClientMessage
//    {
//        public string MapId { get; }

//        public InLeaveMap(string mapId) : base()
//        {
//            MapId = mapId;
//        }
//    }

//    /// <summary>
//    /// Server notification that map join process has started
//    /// </summary>
//    public class OutJoinMapInitiated : ToClientMessage
//    {
//        public string MapId { get; }

//        public OutJoinMapInitiated(string mapId) : base()
//        {
//            MapId = mapId;
//        }
//    }

///// <summary>
///// Server notification that map join has completed successfully
///// </summary>
//public class OutJoinMapCompleted : ToClientMessage
//{
//    public string MapId { get; }
//    public string PlayerId { get; }
//    public MapPosition Position { get; }
//    public TilemapData TilemapData { get; }
//    public IReadOnlyDictionary<string, PlayerMapInfo> PlayerInfo { get; }

//    public OutJoinMapCompleted(string mapId, string playerId, MapPosition position, TilemapData tilemapData, IReadOnlyDictionary<string, PlayerMapInfo> playerInfo) : base()
//    {
//        MapId = mapId;
//        PlayerId = playerId;
//        Position = position;
//        TilemapData = tilemapData;
//        PlayerInfo = playerInfo;
//    }
//}

//    /// <summary>
//    /// Server notification that map join has failed
//    /// </summary>
//    public class OutJoinMapFailed : ToClientMessage
//    {
//        public string MapId { get; }
//        public string Error { get; }

//        public OutJoinMapFailed(string mapId, string error) : base()
//        {
//            MapId = mapId;
//            Error = error;
//        }
//    }

//    /// <summary>
//    /// Server notification that a player has joined the map
//    /// </summary>
//    public class OutPlayerJoinedMap : ToClientMessage
//    {
//        public string PlayerId { get; }
//        public MapPosition? Position { get; }

//        public OutPlayerJoinedMap(string playerId, MapPosition? position) : base()
//        {
//            PlayerId = playerId;
//            Position = position;
//        }
//    }

//    /// <summary>
//    /// Server notification of a player position change
//    /// </summary>
//    public class OutPlayerPositionChange : ToClientMessage
//    {
//        public string PlayerId { get; }
//        public MapPosition? Position { get; }

//        public OutPlayerPositionChange(string playerId, MapPosition? position) : base()
//        {
//            PlayerId = playerId;
//            Position = position;
//        }
//    }

//    /// <summary>
//    /// Server notification that a player has left the map
//    /// </summary>
//    public class OutPlayerLeftMap : ToClientMessage
//    {
//        public string PlayerId { get; }

//        public OutPlayerLeftMap(string playerId) : base()
//        {
//            PlayerId = playerId;
//        }
//    }

//    /// <summary>
//    /// Server notification that map leave process has started
//    /// </summary>
//    public class OutLeaveMapInitiated : ToClientMessage
//    {
//        public string MapId { get; }

//        public OutLeaveMapInitiated(string mapId) : base()
//        {
//            MapId = mapId;
//        }
//    }

//    /// <summary>
//    /// Server notification that map leave has completed successfully
//    /// </summary>
//    public class OutLeaveMapCompleted : ToClientMessage
//    {
//        public string MapId { get; }

//        public OutLeaveMapCompleted(string mapId) : base()
//        {
//            MapId = mapId;
//        }
//    }

//    /// <summary>
//    /// Server notification that map leave has failed
//    /// </summary>
//    public class OutLeaveMapFailed : ToClientMessage
//    {
//        public string MapId { get; }
//        public string Error { get; }

//        public OutLeaveMapFailed(string mapId, string error) : base()
//        {
//            MapId = mapId;
//            Error = error;
//        }
//    }

//    // Movement messages
//    /// <summary>
//    /// Client request to move player to a new position
//    /// </summary>
//    public class InPlayerMove : FromClientMessage
//    {
//        public MapPosition NewPosition { get; }

//        public InPlayerMove(MapPosition newPosition) : base()
//        {
//            NewPosition = newPosition;
//        }
//    }

//    /// <summary>
//    /// Server notification that move process has started
//    /// </summary>
//    public class OutMoveInitiated : ToClientMessage
//    {
//        public MapPosition NewPosition { get; }

//        public OutMoveInitiated(MapPosition newPosition) : base()
//        {
//            NewPosition = newPosition;
//        }
//    }

//    /// <summary>
//    /// Server notification that move has completed successfully
//    /// </summary>
//    public class OutMoveCompleted : ToClientMessage
//    {
//        public MapPosition NewPosition { get; }

//        public OutMoveCompleted(MapPosition newPosition) : base()
//        {
//            NewPosition = newPosition;
//        }
//    }

//    /// <summary>
//    /// Server notification that move has failed
//    /// </summary>
//    public class OutMoveFailed : ToClientMessage
//    {
//        public MapPosition AttemptedPosition { get; }
//        public string Error { get; }

//        public OutMoveFailed(MapPosition attemptedPosition, string error) : base()
//        {
//            AttemptedPosition = attemptedPosition;
//            Error = error;
//        }
//    }

//    // State update messages
//    /// <summary>
//    /// Information about a player's state
//    /// </summary>
//    public class PlayerState
//    {
//        public string Id { get; }
//        public string Name { get; }
//        public MapPosition Position { get; }
//        public string? FightId { get; }

//        public PlayerState(string id, string name, MapPosition position, string? fightId = null)
//        {
//            Id = id;
//            Name = name;
//            Position = position;
//            FightId = fightId;
//        }
//    }

//    /// <summary>
//    /// Server notification with player information
//    /// </summary>
//    public class OutPlayerInfo : ToClientMessage
//    {
//        public PlayerState? State { get; }

//        public OutPlayerInfo(PlayerState? state) : base()
//        {
//            State = state;
//        }
//    }

//    // Map data
//    /// <summary>
//    /// Information about a tilemap
//    /// </summary>
//    public class TilemapData
//    {
//        public int Width { get; }
//        public int Height { get; }
//        public int[] TileData { get; }

//        public TilemapData(int width, int height, int[] tileData)
//        {
//            Width = width;
//            Height = height;
//            TileData = tileData;
//        }
//    }

//    /// <summary>Sent when a player initiates a fight challenge</summary>
//    public class InFightChallengeSend : FromClientMessage
//    {
//        public string TargetId { get; }

//        public InFightChallengeSend(string targetId) : base()
//        {
//            TargetId = targetId;
//        }
//    }
    
//    /// <summary>Received by a player when they are challenged to a fight</summary>
//    public class OutFightChallengeReceived : ToClientMessage
//    {
//        public string ChallengerId { get; }

//        public OutFightChallengeReceived(string challengerId) : base()
//        {
//            ChallengerId = challengerId;
//        }
//    }

//    /// <summary>Sent when a player accepts a fight challenge</summary>
//    public class InFightChallengeAccepted : FromClientMessage
//    {
//        public string TargetId { get; }

//        public InFightChallengeAccepted(string targetId) : base()
//        {
//            TargetId = targetId;
//        }
//    }

//    /// <summary>Notifies players that a fight has begun with both player IDs</summary>
//    public class OutFightStarted : ToClientMessage
//    {
//        public string Player1Id { get; }
//        public string Player2Id { get; }

//        public OutFightStarted(string player1Id, string player2Id) : base()
//        {
//            Player1Id = player1Id;
//            Player2Id = player2Id;
//        }
//    }

//    /// <summary>Notifies players that a fight has ended with a winner and loser</summary>
//    public class OutFightEnded : ToClientMessage
//    {
//        public string WinnerId { get; }
//        public string LoserId { get; }
//        public string Reason { get; }

//        public OutFightEnded(string winnerId, string loserId, string reason) : base()
//        {
//            WinnerId = winnerId;
//            LoserId = loserId;
//            Reason = reason;
//        }
//    }

//    // Card battle system messages
//    /// <summary>Contains essential information about a card for client display</summary>
//    public class CardInfo : ToClientMessage
//    {
//        public string Id { get; }
//        public string Name { get; }
//        public string Description { get; }
//        public int Cost { get; }

//        public CardInfo(string id, string name, string description, int cost) : base()
//        {
//            Id = id;
//            Name = name;
//            Description = description;
//            Cost = cost;
//        }
//    }
    
//    /// <summary>
//    /// Contains information about a status effect for client display
//    /// </summary>
//    public class StatusEffectInfo : ToClientMessage
//    {
//        public string Id { get; }
//        public string Name { get; }
//        public string Description { get; }
//        public int Duration { get; }
//        public string Type { get; }
//        public int Magnitude { get; }

//        public StatusEffectInfo(string id, string name, string description, int duration, string type, int magnitude) : base()
//        {
//            Id = id;
//            Name = name;
//            Description = description;
//            Duration = duration;
//            Type = type;
//            Magnitude = magnitude;
//        }
//    }
    
//    /// <summary>
//    /// Represents a player's current fight status including player ID, HP, AP, cards in hand,
//    /// remaining deck size, discard pile size, and active status effects
//    /// </summary>
//    public class OutPlayerFightState : ToClientMessage
//    {
//        public string PlayerId { get; }
//        public int HitPoints { get; }
//        public int ActionPoints { get; }
//        public List<CardInfo> Hand { get; }
//        public int DeckCount { get; }
//        public int DiscardPileCount { get; }
//        public List<StatusEffectInfo> StatusEffects { get; }

//        public OutPlayerFightState(
//            string playerId,
//            int hitPoints,
//            int actionPoints,
//            List<CardInfo> hand,
//            int deckCount,
//            int discardPileCount,
//            List<StatusEffectInfo> statusEffects = null) : base()
//        {
//            PlayerId = playerId;
//            HitPoints = hitPoints;
//            ActionPoints = actionPoints;
//            Hand = hand;
//            DeckCount = deckCount;
//            DiscardPileCount = discardPileCount;
//            StatusEffects = statusEffects ?? new List<StatusEffectInfo>();
//        }
//    }

//    /// <summary>
//    /// Provides a complete update of the fight state for both players,
//    /// sent after any state-changing action
//    /// </summary>
//    public class OutFightStateUpdate : ToClientMessage
//    {
//        public string CurrentTurnPlayerId { get; }
//        public OutPlayerFightState PlayerState { get; }
//        public OutPlayerFightState OpponentState { get; }

//        public OutFightStateUpdate(
//            string currentTurnPlayerId,
//            OutPlayerFightState playerState,
//            OutPlayerFightState opponentState) : base()
//        {
//            CurrentTurnPlayerId = currentTurnPlayerId;
//            PlayerState = playerState;
//            OpponentState = opponentState;
//        }
//    }

//    /// <summary>
//    /// Notifies that a player's turn has begun
//    /// </summary>
//    public class OutTurnStarted : ToClientMessage
//    {
//        public string ActivePlayerId { get; }

//        public OutTurnStarted(string activePlayerId) : base()
//        {
//            ActivePlayerId = activePlayerId;
//        }
//    }

//    /// <summary>Notifies that a player's turn has ended</summary>
//    public class OutTurnEnded : ToClientMessage
//    {
//        public string PlayerId { get; }

//        public OutTurnEnded(string playerId) : base()
//        {
//            PlayerId = playerId;
//        }
//    }

//    /// <summary>Request from client to play a specific card</summary>
//    public class InPlayCard : FromClientMessage
//    {
//        public string CardId { get; }

//        public InPlayCard(string cardId) : base()
//        {
//            CardId = cardId;
//        }
//    }
    
//    /// <summary>Confirms that a card play attempt has been received</summary>
//    public class OutCardPlayInitiated : ToClientMessage
//    {
//        public string CardId { get; }

//        public OutCardPlayInitiated(string cardId) : base()
//        {
//            CardId = cardId;
//        }
//    }
    
//    /// <summary>
//    /// Notifies that a card was successfully played and describes its effect
//    /// </summary>
//    public class OutCardPlayCompleted : ToClientMessage
//    {
//        public string PlayerId { get; }
//        public CardInfo PlayedCard { get; }
//        public string Effect { get; }
//        public bool IsVisible { get; }

//        public OutCardPlayCompleted(
//            string playerId,
//            CardInfo playedCard,
//            string effect,
//            bool isVisible = true) : base()
//        {
//            PlayerId = playerId;
//            PlayedCard = playedCard;
//            Effect = effect;
//            IsVisible = isVisible;
//        }
//    }
    
//    /// <summary>
//    /// Notifies that a card play attempt failed and provides the reason
//    /// </summary>
//    public class OutCardPlayFailed : ToClientMessage
//    {
//        public string CardId { get; }
//        public string Error { get; }

//        public OutCardPlayFailed(string cardId, string error) : base()
//        {
//            CardId = cardId;
//            Error = error;
//        }
//    }

//    /// <summary>
//    /// Notifies that a card effect was applied to a player
//    /// (e.g. damage, healing, status effects)
//    /// </summary>
//    public class OutEffectApplied : ToClientMessage
//    {
//        public string TargetPlayerId { get; }
//        public string EffectType { get; }
//        public int Value { get; }
//        public string Source { get; }

//        public OutEffectApplied(
//            string targetPlayerId,
//            string effectType,
//            int value,
//            string source) : base()
//        {
//            TargetPlayerId = targetPlayerId;
//            EffectType = effectType;
//            Value = value;
//            Source = source;
//        }
//    }
    
//    /// <summary>
//    /// Sends SVG data for multiple cards to the client
//    /// </summary>
//    public class OutCardImages : ToClientMessage
//    {
//        public Dictionary<string, string> CardSvgData { get; }

//        public OutCardImages(Dictionary<string, string> cardSvgData) : base()
//        {
//            CardSvgData = cardSvgData;
//        }
//    }
    
//    /// <summary>
//    /// Notifies when a new card is drawn with its SVG data
//    /// </summary>
//    public class OutCardDrawn : ToClientMessage
//    {
//        public CardInfo CardInfo { get; }
//        public string SvgData { get; }

//        public OutCardDrawn(CardInfo cardInfo, string svgData) : base()
//        {
//            CardInfo = cardInfo;
//            SvgData = svgData;
//        }
//    }
    
//    /// <summary>
//    /// Request from client to end their turn
//    /// </summary>
//    public class InEndTurn : FromClientMessage
//    {
//        public InEndTurn() : base() { }
//    }
//}
