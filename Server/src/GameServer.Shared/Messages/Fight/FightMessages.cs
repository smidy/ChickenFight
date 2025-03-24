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
    /// Server notification that a fight challenge was received
    /// </summary>
    public class ExtFightChallengeReceived : ExtServerMessage, IExtNotification
    {
        public string ChallengerId { get; }

        public ExtFightChallengeReceived(string challengerId) : base()
        {
            ChallengerId = challengerId;
        }
    }

    /// <summary>
    /// Client request to accept a fight challenge
    /// </summary>
    public class ExtFightChallengeAccepted : ExtClientMessage, IExtRequest
    {
        public string TargetId { get; }

        public ExtFightChallengeAccepted(string targetId) : base()
        {
            TargetId = targetId;
        }
    }

    /// <summary>
    /// Server notification that a fight has started
    /// </summary>
    public class ExtFightStarted : ExtServerMessage, IExtNotification
    {
        public string Player1Id { get; }
        public string Player2Id { get; }

        public ExtFightStarted(string player1Id, string player2Id) : base()
        {
            Player1Id = player1Id;
            Player2Id = player2Id;
        }
    }

    /// <summary>
    /// Server notification that a fight has ended
    /// </summary>
    public class ExtFightEnded : ExtServerMessage, IExtNotification
    {
        public string WinnerId { get; }
        public string LoserId { get; }
        public string Reason { get; }

        public ExtFightEnded(string winnerId, string loserId, string reason) : base()
        {
            WinnerId = winnerId;
            LoserId = loserId;
            Reason = reason;
        }
    }
}
