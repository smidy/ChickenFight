using Proto;
using GameServer.Application.Models;
using GameServer.Shared.Models;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Messages.Internal
{
    // Player management messages
    public record JoinMap(PID PlayerActor, string PlayerName, PID Requester);
    public record PlayerAddedToMap(PID PlayerActor,
        PID MapPID,
        string MapId, 
        MapPosition StartPosition, 
        Shared.ExternalMessages.TilemapData TilemapData,
        IReadOnlyDictionary<PID, MapPosition> PlayerPositions);
    public record PlayerAddFailure(PID PlayerActor, string MapId, string Error);
    
    public record LeaveMap(PID PlayerActor, PID Requester);
    public record PlayerRemovedFromMap(string MapId, PID PlayerActor);
    public record PlayerRemoveFailure(string MapId, PID PlayerActor, string Error);
    
    // Movement messages
    public record TryMove(PID PlayerActor, MapPosition NewPosition, PID Requester);
    public record MoveValidated(PID PlayerActor, MapPosition NewPosition);
    public record MoveRejected(PID PlayerActor, MapPosition AttemptedPosition, string Error);
    
    // Map state messages
    public record MapStateUpdate(string MapId, string MapName, int Width, int Height);

    public record BroadcastExternalMessage(ToClientMessage ExternalMessage);
}
