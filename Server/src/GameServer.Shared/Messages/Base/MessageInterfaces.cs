namespace GameServer.Shared.Messages.Base
{
    /// <summary>
    /// Interface for messages that represent requests
    /// </summary>
    public interface IExtRequest
    {
    }

    /// <summary>
    /// Interface for messages that represent responses to requests
    /// </summary>
    public interface IExtResponse
    {
        bool Success { get; }
        string ErrorMessage { get; }
    }

    /// <summary>
    /// Interface for messages that represent notifications (no response expected)
    /// </summary>
    public interface IExtNotification
    {
    }

    /// <summary>
    /// Interface for messages related to a specific map
    /// </summary>
    public interface IExtMapRelated
    {
        string MapId { get; }
    }

    /// <summary>
    /// Interface for messages related to a specific player
    /// </summary>
    public interface IExtPlayerRelated
    {
        string PlayerId { get; }
    }
}
