using GameServer.Shared.Models;

namespace GameServer.Shared.Messages
{
    public delegate Task SendToClientDelegate<in T>(T message) where T : BaseMessage;

    public record BaseMessage
    {
        public string Type { get; }
        protected BaseMessage(string type) => Type = type;
    }

    // Connection messages
    public record ExtConnectionConfirmed : BaseMessage 
    {
        public ExtConnectionConfirmed(string sessionId) : base(nameof(ExtJoinMapFailed))
        {
            this.SessionId = sessionId;
        }

        public string SessionId { get; }
    }

    // Map listing messages
    public record ExtRequestMapList : BaseMessage
    {
        public ExtRequestMapList() : base(nameof(ExtRequestMapList)) {}
    }
    public record RequestMapListResponse : BaseMessage
    {
        public RequestMapListResponse(List<MapInfo> maps) : base(nameof(RequestMapListResponse))
        {
            Maps = maps;
        }

        public List<MapInfo> Maps { get; }
    }
    public record MapInfo(string Id, string Name, int Width, int Height, int PlayerCount);

    // Map join/leave messages
    public record ExtJoinMap : BaseMessage
    {
        public string MapId { get; }
        public ExtJoinMap(string mapId) : base(nameof(ExtJoinMap)) => MapId = mapId;
    }
    
    public record ExtJoinMapInitiated : BaseMessage
    {
        public string MapId { get; }
        public ExtJoinMapInitiated(string mapId) : base(nameof(ExtJoinMapInitiated)) => MapId = mapId;
    }
    
    public record ExtJoinMapCompleted : BaseMessage
    {
        public string MapId { get; }
        public TilemapData TilemapData { get; }
        public ExtJoinMapCompleted(string mapId, TilemapData tilemapData) : base(nameof(ExtJoinMapCompleted))
        {
            MapId = mapId;
            TilemapData = tilemapData;
        }
    }
    
    public record ExtJoinMapFailed : BaseMessage
    {
        public string MapId { get; }
        public string Error { get; }
        public ExtJoinMapFailed(string mapId, string error) : base(nameof(ExtJoinMapFailed))
        {
            MapId = mapId;
            Error = error;
        }
    }

    public record ExtLeaveMap : BaseMessage
    {
        public string MapId { get; }
        public ExtLeaveMap(string mapId) : base(nameof(ExtLeaveMap)) => MapId = mapId;
    }
    
    public record ExtLeaveMapInitiated : BaseMessage
    {
        public string MapId { get; }
        public ExtLeaveMapInitiated(string mapId) : base(nameof(ExtLeaveMapInitiated)) => MapId = mapId;
    }
    
    public record ExtLeaveMapCompleted : BaseMessage
    {
        public string MapId { get; }
        public ExtLeaveMapCompleted(string mapId) : base(nameof(ExtLeaveMapCompleted)) => MapId = mapId;
    }
    
    public record ExtLeaveMapFailed : BaseMessage
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
    public record ExtMove : BaseMessage
    {
        public Position NewPosition { get; }
        public ExtMove(Position newPosition) : base(nameof(ExtMove)) => NewPosition = newPosition;
    }
    
    public record ExtMoveInitiated : BaseMessage
    {
        public Position NewPosition { get; }
        public ExtMoveInitiated(Position newPosition) : base(nameof(ExtMoveInitiated)) => NewPosition = newPosition;
    }
    
    public record ExtMoveCompleted : BaseMessage
    {
        public Position NewPosition { get; }
        public ExtMoveCompleted(Position newPosition) : base(nameof(ExtMoveCompleted)) => NewPosition = newPosition;
    }
    
    public record ExtMoveFailed : BaseMessage
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
    public record ExtPlayerInfo : BaseMessage
    {
        public ExtPlayerInfo(PlayerState? state) : base(nameof(ExtPlayerInfo))
        {
            State = state;
        }

        public PlayerState? State { get; }
    }

    // Map data
    public record TilemapData(int Width, int Height, int[] TileData);
}
