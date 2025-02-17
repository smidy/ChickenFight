using GameServer.Shared.Models;

namespace GameServer.Shared.Messages
{
    public delegate Task SendToClientDelegate<in T>(T message) where T : BaseExternalMessage;

    public record BaseExternalMessage
    {

    }

    // Connection messages
    public record ExtConnectionConfirmed(string SessionId) : BaseExternalMessage;

    // Map listing messages
    public record ExtRequestMapList() : BaseExternalMessage;
    public record RequestMapListResponse(List<MapInfo> Maps) : BaseExternalMessage;
    public record MapInfo(string Id, string Name, int Width, int Height, int PlayerCount);

    // Map join/leave messages
    public record ExtJoinMap(string MapId) : BaseExternalMessage;
    
    public record ExtJoinMapInitiated(string MapId) : BaseExternalMessage;
    
    public record ExtJoinMapCompleted(string MapId, TilemapData TilemapData) : BaseExternalMessage;
    
    public record ExtJoinMapFailed(string MapId, string Error) : BaseExternalMessage;

    public record ExtPlayerJoinedMap(string PlayerId, Position? Position) : BaseExternalMessage;

    public record ExtPlayerPositionChange(string PlayerId, Position? Position) : BaseExternalMessage;

    public record ExtPlayerLeftMap(string PlayerId) : BaseExternalMessage;

    public record ExtLeaveMap(string MapId) : BaseExternalMessage;
    
    public record ExtLeaveMapInitiated(string MapId) : BaseExternalMessage;
    
    public record ExtLeaveMapCompleted(string MapId) : BaseExternalMessage;
    
    public record ExtLeaveMapFailed(string MapId, string Error) : BaseExternalMessage;

    // Movement messages
    public record ExtMove(Position NewPosition) : BaseExternalMessage;
    
    public record ExtMoveInitiated(Position NewPosition) : BaseExternalMessage;
    
    public record ExtMoveCompleted(Position NewPosition) : BaseExternalMessage;
    
    public record ExtMoveFailed(Position AttemptedPosition, string Error) : BaseExternalMessage;

    // State update messages
    public record PlayerState(string Id, string Name, Position Position);
    public record ExtPlayerInfo(PlayerState? State) : BaseExternalMessage;

    // Map data
    public record TilemapData(int Width, int Height, int[] TileData);

    // External messages for client communication
    public record ExtFightChallengeReceived(string ChallengerId) : BaseExternalMessage;

    public record ExtFightChallengeAccepted(string TargetId) : BaseExternalMessage;

    public record ExtFightStarted(string OpponentId) : BaseExternalMessage;

    public record ExtFightEnded(string WinnerId, string Reason) : BaseExternalMessage;
}
