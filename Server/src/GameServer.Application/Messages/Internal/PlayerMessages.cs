using Proto;
using GameServer.Application.Models;
using GameServer.Shared.Models;

namespace GameServer.Application.Messages.Internal
{
    // Map join/leave messages
    public record JoinMapRequest(string MapId);
    public record JoinMapInitiated(string MapId);
    public record JoinMapCompleted(string MapId, Shared.ExternalMessages.TilemapData TilemapData);
    public record JoinMapFailed(string MapId, string Error);
    
    public record LeaveMapRequest(string? MapId);
    public record LeaveMapInitiated(string MapId);
    public record LeaveMapCompleted(string MapId);
    public record LeaveMapFailed(string MapId, string Error);
    
    // Movement messages
    public record MoveRequest(Position NewPosition);
    public record MoveInitiated(Position NewPosition);
    public record MoveCompleted(Position NewPosition);
    public record MoveFailed(Position AttemptedPosition, string Error);
    
    // State update messages
    public record PlayerStateUpdate(Player Player);
    public record PlayerStateUpdateReceived(Player Player);
}
