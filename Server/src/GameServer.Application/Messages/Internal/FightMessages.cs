using Proto;
using GameServer.Application.Models;

namespace GameServer.Application.Messages.Internal
{
    // Fight challenge messages
    public record ChallengeFightRequest(string ChallengerId, string TargetId);
    public record FightChallengeResponse(string ChallengerId, string TargetId, bool Accepted);
    
    // Fight state messages
    public record FightStarted(string FightId, string Player1Id, string Player2Id);
    public record FightCompleted(string FightId, string WinnerId, string LoserId, string Reason);
    public record PlayerDisconnected(string PlayerId);
    public record EndFight(string WinnerId, string LoserId, string Reason);

}
