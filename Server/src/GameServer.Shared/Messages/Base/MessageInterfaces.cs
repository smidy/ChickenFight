namespace GameServer.Shared.Messages.Base
{
    /// <summary>
    /// Interface for messages that represent requests
    /// </summary>
    public interface IRequest
    {
    }

    /// <summary>
    /// Interface for messages that represent responses to requests
    /// </summary>
    public interface IResponse
    {
        bool Success { get; }
        string ErrorMessage { get; }
    }

    /// <summary>
    /// Interface for messages that represent notifications (no response expected)
    /// </summary>
    public interface INotification
    {
    }

    /// <summary>
    /// Interface for messages related to a specific map
    /// </summary>
    public interface IMapRelated
    {
        string MapId { get; }
    }

    /// <summary>
    /// Interface for messages related to a specific player
    /// </summary>
    public interface IPlayerRelated
    {
        string PlayerId { get; }
    }
}
