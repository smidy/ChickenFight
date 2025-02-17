using Proto;
using GameServer.Application.Models;
using GameServer.Shared.Models;

namespace GameServer.Application.Messages.Internal
{
    // Player management messages
    public record AddPlayer(PID PlayerActor, Player Player, PID Requester);
    public record PlayerAddedToMap(PID PlayerActor, PID MapPID, string MapId, Position StartPosition, Shared.ExternalMessages.TilemapData TilemapData);
    public record PlayerAddFailure(PID PlayerActor, string MapId, string Error);
    
    public record RemovePlayer(string PlayerId, PID Requester);
    public record PlayerRemovedFromMap(string MapId, string PlayerId);
    public record PlayerRemoveFailure(string MapId, string PlayerId, string Error);
    
    // Movement messages
    public record ValidateMove(string PlayerId, Position NewPosition, PID Requester);
    public record MoveValidated(string PlayerId, Position NewPosition);
    public record MoveRejected(string PlayerId, Position AttemptedPosition, string Error);
    
    // Map state messages
    public record MapStateUpdate(Map Map);
    public record SubscribeToMapUpdates(PID Subscriber);
    public record UnsubscribeFromMapUpdates(PID Subscriber);
    public record MapUpdateSubscribed(PID Subscriber);
    public record MapUpdateUnsubscribed(PID Subscriber);
}
