using Proto;
using GameServer.Application.Models;

namespace GameServer.Application.Messages.Internal
{
    // Fight challenge messages
    public record ChallengeFightRequest(string ChallengerId, Player Challenger, string TargetId);
    public record FightChallengeResponse(string ChallengerId, Player Challenger, string TargetId, Player Target, bool Accepted);
    
    // Fight state messages
    public record FightStarted(string FightId, string Player1Id, Player Player1, string Player2Id, Player Player2);
    public record FightCompleted(string FightId, string WinnerId, string LoserId, string Reason);
    public record PlayerDisconnected(string PlayerId);
    public record EndFight(string WinnerId, string LoserId, string Reason);

    // Card battle messages
    public record StartTurn(string PlayerId);
    public record EndTurn(string PlayerId);
    public record PlayCard(string PlayerId, string CardId);
    public record CardPlayed(string PlayerId, Card Card, string Effect);
    public record ApplyCardEffect(string TargetPlayerId, string EffectType, int Value, string Source);
}
