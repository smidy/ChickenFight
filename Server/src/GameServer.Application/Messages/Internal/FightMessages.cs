using Proto;
using GameServer.Application.Models;

namespace GameServer.Application.Messages.Internal
{
    // Fight challenge messages
    public record FightChallengeRequest(PID ChallengerActor, Player Challenger, PID TargetActor);
    public record FightChallengeResponse(PID ChallengerActor, Player Challenger, PID TargetActor, Player Target, bool Accepted);
    
    // Fight state messages
    public record FightStarted(PID FightActor, PID Player1Actor, Player Player1, PID Player2Actor, Player Player2);
    public record FightCompleted(PID FightId, PID WinnerActor, PID LoserActor, string Reason);
    public record PlayerDisconnected(PID PlayerActor);
    public record EndFight(PID WinnerActor, PID LoserActor, string Reason);

    // Card battle messages
    public record StartTurn(PID PlayerActor);
    public record EndTurn(PID PlayerActor);
    public record PlayCard(PID PlayerActor, string CardId);
    public record CardPlayed(PID PlayerActor, Card Card, string Effect);
    public record ApplyCardEffect(PID TargetActor, string EffectType, int Value, string Source);
}
