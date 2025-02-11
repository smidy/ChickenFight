using GameServer.Shared.Models;

namespace GameServer.Shared.Messages
{
    public delegate Task SendToClientDelegate<in T>(T message);

    public record BaseMessage
    {
        public string Type { get; }
        protected BaseMessage(string type) => Type = type;
    }

    // Connection messages
    public record ConnectionConfirmed(string SessionId);

    // Map listing messages
    public record RequestMapList : BaseMessage
    {
        public RequestMapList() : base(nameof(RequestMapList)) {}
    }
    public record RequestMapListResponse(List<MapInfo> Maps);
    public record MapInfo(string Id, string Name, int Width, int Height, int PlayerCount);

    // Map join/leave messages
    public record JoinMap : BaseMessage
    {
        public string MapId { get; }
        public JoinMap(string mapId) : base(nameof(JoinMap)) => MapId = mapId;
    }
    
    public record JoinMapInitiated : BaseMessage
    {
        public string MapId { get; }
        public JoinMapInitiated(string mapId) : base(nameof(JoinMapInitiated)) => MapId = mapId;
    }
    
    public record JoinMapCompleted : BaseMessage
    {
        public string MapId { get; }
        public TilemapData TilemapData { get; }
        public JoinMapCompleted(string mapId, TilemapData tilemapData) : base(nameof(JoinMapCompleted))
        {
            MapId = mapId;
            TilemapData = tilemapData;
        }
    }
    
    public record JoinMapFailed : BaseMessage
    {
        public string MapId { get; }
        public string Error { get; }
        public JoinMapFailed(string mapId, string error) : base(nameof(JoinMapFailed))
        {
            MapId = mapId;
            Error = error;
        }
    }

    public record LeaveMap : BaseMessage
    {
        public string MapId { get; }
        public LeaveMap(string mapId) : base(nameof(LeaveMap)) => MapId = mapId;
    }
    
    public record LeaveMapInitiated : BaseMessage
    {
        public string MapId { get; }
        public LeaveMapInitiated(string mapId) : base(nameof(LeaveMapInitiated)) => MapId = mapId;
    }
    
    public record LeaveMapCompleted : BaseMessage
    {
        public string MapId { get; }
        public LeaveMapCompleted(string mapId) : base(nameof(LeaveMapCompleted)) => MapId = mapId;
    }
    
    public record LeaveMapFailed : BaseMessage
    {
        public string MapId { get; }
        public string Error { get; }
        public LeaveMapFailed(string mapId, string error) : base(nameof(LeaveMapFailed))
        {
            MapId = mapId;
            Error = error;
        }
    }

    // Movement messages
    public record Move : BaseMessage
    {
        public Position NewPosition { get; }
        public Move(Position newPosition) : base(nameof(Move)) => NewPosition = newPosition;
    }
    
    public record MoveInitiated : BaseMessage
    {
        public Position NewPosition { get; }
        public MoveInitiated(Position newPosition) : base(nameof(MoveInitiated)) => NewPosition = newPosition;
    }
    
    public record MoveCompleted : BaseMessage
    {
        public Position NewPosition { get; }
        public MoveCompleted(Position newPosition) : base(nameof(MoveCompleted)) => NewPosition = newPosition;
    }
    
    public record MoveFailed : BaseMessage
    {
        public Position AttemptedPosition { get; }
        public string Error { get; }
        public MoveFailed(Position attemptedPosition, string error) : base(nameof(MoveFailed))
        {
            AttemptedPosition = attemptedPosition;
            Error = error;
        }
    }

    // State update messages
    public record PlayerState(string Id, string Name, Position Position);
    public record PlayerInfo(PlayerState? State);

    // Map data
    public record TilemapData(int Width, int Height, int[] TileData);
}
