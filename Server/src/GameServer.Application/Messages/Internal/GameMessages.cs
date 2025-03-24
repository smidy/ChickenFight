using Proto;
using GameServer.Application.Actors;
using GameServer.Shared.Messages.Base;

namespace GameServer.Application.Messages.Internal
{
    // Creation messages
    public record CreatePlayer(string PlayerId, string PlayerName, SendToClientDelegate<ExtServerMessage> SendToClient);
    public record CreatePlayerResponse(PID PlayerActor);
}
