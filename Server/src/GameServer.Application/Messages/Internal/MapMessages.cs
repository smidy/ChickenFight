using Proto;
using GameServer.Application.Models;
using GameServer.Shared.Models;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Messages.Internal
{
    // Player management messages
    public record AddPlayer(PID PlayerActor, string PlayerName, PID Requester);
    public record PlayerAddedToMap(PID PlayerActor,
        PID MapPID,
        string MapId, 
        ExPosition StartPosition, 
        Shared.ExternalMessages.TilemapData TilemapData,
        IReadOnlyDictionary<PID, ExPosition> PlayerPositions);
    public record PlayerAddFailure(PID PlayerActor, string MapId, string Error);
    
    public record RemovePlayer(PID PlayerActor, PID Requester);
    public record PlayerRemovedFromMap(string MapId, PID PlayerActor);
    public record PlayerRemoveFailure(string MapId, PID PlayerActor, string Error);
    
    // Movement messages
    public record ValidateMove(PID PlayerActor, ExPosition NewPosition, PID Requester);
    public record MoveValidated(PID PlayerActor, ExPosition NewPosition);
    public record MoveRejected(PID PlayerActor, ExPosition AttemptedPosition, string Error);
    
    // Map state messages
    public record MapStateUpdate(string MapId, string MapName, int Width, int Height);

    public record BroadcastExternalMessage(ToClientMessage ExternalMessage);
}
