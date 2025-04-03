using GameServer.Shared.Messages.Base;

namespace GameServer.Shared.Messages.Fight
{
    /// <summary>
    /// Client request to send a fight challenge to another player
    /// </summary>
    public class ExtFightChallengeRequest : ExtClientMessage, IExtRequest
    {
        public string TargetId { get; }

        public ExtFightChallengeRequest(string targetId) : base()
        {
            TargetId = targetId;
        }
    }


    /// <summary>
    /// Server notification that a fight has started
    /// </summary>
    public class ExtFightStarted : ExtServerMessage, IExtNotification
    {
        public string FightId { get; }
        public string Player1Id { get; }
        public string Player2Id { get; }

        public ExtFightStarted(string fightId, string player1Id, string player2Id) : base()
        {
            FightId = fightId;
            Player1Id = player1Id;
            Player2Id = player2Id;
        }
    }

    /// <summary>
    /// Server notification that a fight has ended
    /// </summary>
    public class ExtFightEnded : ExtServerMessage, IExtNotification
    {
        public string FightId { get; }
        public string WinnerId { get; }
        public string LoserId { get; }
        public string Reason { get; }

        public ExtFightEnded(string fightId, string winnerId, string loserId, string reason) : base()
        {
            FightId = fightId;
            WinnerId = winnerId;
            LoserId = loserId;
            Reason = reason;
        }
    }
}
