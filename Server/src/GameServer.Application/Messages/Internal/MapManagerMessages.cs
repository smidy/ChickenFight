using GameServer.Application.Models;
using Proto;

namespace GameServer.Application.Messages.Internal
{
    public record RequestMapList(PID Requester);
    public record MapListResponse(IReadOnlyCollection<Map> Maps);
}
