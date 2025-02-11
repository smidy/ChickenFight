using Proto;
using GameServer.Application.Models;
using GameServer.Shared.Messages;

namespace GameServer.Application.Messages.Internal
{
    // Creation messages
    public record CreatePlayer(string ConnectionId, string PlayerName, SendToClientDelegate<object> SendToClient);
    public record PlayerCreated(PID PlayerActor, Player Player);
    
    // Map listing messages
    public record GetMapList;
    public record MapList(IReadOnlyCollection<Map> Maps);
    
    // Async map PID resolution messages
    public record GetMapPid(string MapId, PID Requester);
    public record MapPidResolved(string MapId, PID? MapPid, PID Requester);
    public record MapPidNotFound(string MapId, PID Requester);
}
