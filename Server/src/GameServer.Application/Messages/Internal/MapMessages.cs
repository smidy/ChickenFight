using Proto;
using GameServer.Application.Models;
using GameServer.Shared.Models;

namespace GameServer.Application.Messages.Internal
{
    // Player management messages
    public record AddPlayer(PID PlayerActor, string PlayerId, string PlayerName, PID Requester);
    public record PlayerAddedToMap(PID PlayerActor,
        PID MapPID, 
        string PlayerId,
        string MapId, 
        Position StartPosition, 
        Shared.ExternalMessages.TilemapData TilemapData,
        IReadOnlyDictionary<string, Position> PlayerPositions);
    public record PlayerAddFailure(PID PlayerActor, string MapId, string Error);
    
    public record RemovePlayer(string PlayerId, PID Requester);
    public record PlayerRemovedFromMap(string MapId, string PlayerId);
    public record PlayerRemoveFailure(string MapId, string PlayerId, string Error);
    
    // Movement messages
    public record ValidateMove(string PlayerId, Position NewPosition, PID Requester);
    public record MoveValidated(string PlayerId, Position NewPosition);
    public record MoveRejected(string PlayerId, Position AttemptedPosition, string Error);
    
    // Map state messages
    public record MapStateUpdate(string MapId, string MapName, int Width, int Height);
}
