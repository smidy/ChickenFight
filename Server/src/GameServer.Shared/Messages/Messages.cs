using GameServer.Shared.Models;

namespace GameServer.Shared.Messages
{
    public delegate Task SendToClientDelegate<in T>(T message) where T : BaseExternalMessage;

    public record BaseExternalMessage
    {
        public string Type { get; }
        protected BaseExternalMessage(string type) => Type = type;
    }

    // Connection messages
    public record ExtConnectionConfirmed : BaseExternalMessage 
    {
        public ExtConnectionConfirmed(string sessionId) : base(nameof(ExtJoinMapFailed))
        {
            this.SessionId = sessionId;
        }

        public string SessionId { get; }
    }

    // Map listing messages
    public record ExtRequestMapList : BaseExternalMessage
    {
        public ExtRequestMapList() : base(nameof(ExtRequestMapList)) {}
    }
    public record RequestMapListResponse : BaseExternalMessage
    {
        public RequestMapListResponse(List<MapInfo> maps) : base(nameof(RequestMapListResponse))
        {
            Maps = maps;
        }

        public List<MapInfo> Maps { get; }
    }
    public record MapInfo(string Id, string Name, int Width, int Height, int PlayerCount);

    // Map join/leave messages
    public record ExtJoinMap : BaseExternalMessage
    {
        public string MapId { get; }
        public ExtJoinMap(string mapId) : base(nameof(ExtJoinMap)) => MapId = mapId;
    }
    
    public record ExtJoinMapInitiated : BaseExternalMessage
    {
        public string MapId { get; }
        public ExtJoinMapInitiated(string mapId) : base(nameof(ExtJoinMapInitiated)) => MapId = mapId;
    }
    
    public record ExtJoinMapCompleted : BaseExternalMessage
    {
        public string MapId { get; }
        public TilemapData TilemapData { get; }
        public ExtJoinMapCompleted(string mapId, TilemapData tilemapData) : base(nameof(ExtJoinMapCompleted))
        {
            MapId = mapId;
            TilemapData = tilemapData;
        }
    }
    
    public record ExtJoinMapFailed : BaseExternalMessage
    {
        public string MapId { get; }
        public string Error { get; }
        public ExtJoinMapFailed(string mapId, string error) : base(nameof(ExtJoinMapFailed))
        {
            MapId = mapId;
            Error = error;
        }
    }

    public record ExtPlayerJoinedMap : BaseExternalMessage
    {
        public ExtPlayerJoinedMap(string playerId, Position? position) : base(nameof(ExtPlayerJoinedMap))
        {
            PlayerId = playerId;
            Position = position;
        }

        public string PlayerId { get; }
        public Position? Position { get; private set; }

    }

    public record ExtPlayerPositionChange : BaseExternalMessage
    {
        public ExtPlayerPositionChange(string playerId, Position? position) : base(nameof(ExtPlayerPositionChange))
        {
            PlayerId = playerId;
            Position = position;
        }

        public string PlayerId { get; }
        public Position? Position { get; private set; }

    }

    public record ExtPlayerLeftMap : BaseExternalMessage
    {
        public ExtPlayerLeftMap(string playerId) : base(nameof(ExtPlayerLeftMap))
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }
    }

    public record ExtLeaveMap : BaseExternalMessage
    {
        public string MapId { get; }
        public ExtLeaveMap(string mapId) : base(nameof(ExtLeaveMap)) => MapId = mapId;
    }
    
    public record ExtLeaveMapInitiated : BaseExternalMessage
    {
        public string MapId { get; }
        public ExtLeaveMapInitiated(string mapId) : base(nameof(ExtLeaveMapInitiated)) => MapId = mapId;
    }
    
    public record ExtLeaveMapCompleted : BaseExternalMessage
    {
        public string MapId { get; }
        public ExtLeaveMapCompleted(string mapId) : base(nameof(ExtLeaveMapCompleted)) => MapId = mapId;
    }
    
    public record ExtLeaveMapFailed : BaseExternalMessage
    {
        public string MapId { get; }
        public string Error { get; }
        public ExtLeaveMapFailed(string mapId, string error) : base(nameof(ExtLeaveMapFailed))
        {
            MapId = mapId;
            Error = error;
        }
    }

    // Movement messages
    public record ExtMove : BaseExternalMessage
    {
        public Position NewPosition { get; }
        public ExtMove(Position newPosition) : base(nameof(ExtMove)) => NewPosition = newPosition;
    }
    
    public record ExtMoveInitiated : BaseExternalMessage
    {
        public Position NewPosition { get; }
        public ExtMoveInitiated(Position newPosition) : base(nameof(ExtMoveInitiated)) => NewPosition = newPosition;
    }
    
    public record ExtMoveCompleted : BaseExternalMessage
    {
        public Position NewPosition { get; }
        public ExtMoveCompleted(Position newPosition) : base(nameof(ExtMoveCompleted)) => NewPosition = newPosition;
    }
    
    public record ExtMoveFailed : BaseExternalMessage
    {
        public Position AttemptedPosition { get; }
        public string Error { get; }
        public ExtMoveFailed(Position attemptedPosition, string error) : base(nameof(ExtMoveFailed))
        {
            AttemptedPosition = attemptedPosition;
            Error = error;
        }
    }

    // State update messages
    public record PlayerState(string Id, string Name, Position Position);
    public record ExtPlayerInfo : BaseExternalMessage
    {
        public ExtPlayerInfo(PlayerState? state) : base(nameof(ExtPlayerInfo))
        {
            State = state;
        }

        public PlayerState? State { get; }
    }

    // Map data
    public record TilemapData(int Width, int Height, int[] TileData);

    // External messages for client communication
    public record ExtFightChallengeReceived : BaseExternalMessage
    {
        public ExtFightChallengeReceived(string ChallengerId) : base(nameof(ExtFightChallengeReceived))
        {
            this.ChallengerId = ChallengerId;
        }

        public string ChallengerId { get; }
    }

    public record ExtFightChallengeAccepted : BaseExternalMessage
    {
        public ExtFightChallengeAccepted(string TargetId) : base(nameof(ExtFightChallengeAccepted))
        {
            this.TargetId = TargetId;
        }

        public string TargetId { get; }
    }

    public record ExtFightStarted : BaseExternalMessage
    {
        public ExtFightStarted(string OpponentId) : base(nameof(ExtFightStarted))
        {
            this.OpponentId = OpponentId;
        }

        public string OpponentId { get; }
    }

    public record ExtFightEnded : BaseExternalMessage
    {
        public ExtFightEnded(string WinnerId, string Reason) : base(nameof(ExtFightEnded))
        {
            this.WinnerId = WinnerId;
            this.Reason = Reason;
        }

        public string WinnerId { get; }
        public string Reason { get; }
    }
}
