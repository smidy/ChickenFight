using GameServer.Shared.Messages.Base;

namespace GameServer.Shared.Messages.Fight
{
    /// <summary>
    /// Client request to send a fight challenge to another player
    /// </summary>
    public class FightChallengeRequest : ClientMessage, IRequest
    {
        public string TargetId { get; }

        public FightChallengeRequest(string targetId) : base()
        {
            TargetId = targetId;
        }
    }

    /// <summary>
    /// Server notification that a fight challenge was received
    /// </summary>
    public class FightChallengeReceived : ServerMessage, INotification
    {
        public string ChallengerId { get; }

        public FightChallengeReceived(string challengerId) : base()
        {
            ChallengerId = challengerId;
        }
    }

    /// <summary>
    /// Client request to accept a fight challenge
    /// </summary>
    public class FightChallengeAccepted : ClientMessage, IRequest
    {
        public string TargetId { get; }

        public FightChallengeAccepted(string targetId) : base()
        {
            TargetId = targetId;
        }
    }

    /// <summary>
    /// Server notification that a fight has started
    /// </summary>
    public class FightStarted : ServerMessage, INotification
    {
        public string Player1Id { get; }
        public string Player2Id { get; }

        public FightStarted(string player1Id, string player2Id) : base()
        {
            Player1Id = player1Id;
            Player2Id = player2Id;
        }
    }

    /// <summary>
    /// Server notification that a fight has ended
    /// </summary>
    public class FightEnded : ServerMessage, INotification
    {
        public string WinnerId { get; }
        public string LoserId { get; }
        public string Reason { get; }

        public FightEnded(string winnerId, string loserId, string reason) : base()
        {
            WinnerId = winnerId;
            LoserId = loserId;
            Reason = reason;
        }
    }
}
