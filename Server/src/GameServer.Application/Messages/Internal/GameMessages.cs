using Proto;
using GameServer.Application.Models;
using GameServer.Shared.ExternalMessages;
using GameServer.Application.Actors;

namespace GameServer.Application.Messages.Internal
{
    // Creation messages
    public record CreatePlayer(string PlayerId, string PlayerName, SendToClientDelegate<ToClientMessage> SendToClient);
    public record CreatePlayerResponse(PID PlayerActor);
     
}
