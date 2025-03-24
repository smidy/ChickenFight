using GameServer.Shared.Messages.Base;

namespace GameServer.Shared.Messages.Connection
{
    /// <summary>
    /// Client request for connection confirmation
    /// </summary>
    public class PlayerIdRequest : ClientMessage, IRequest
    {
        public PlayerIdRequest() : base() { }
    }

    /// <summary>
    /// Server response with connection confirmation
    /// </summary>
    public class PlayerIdResponse : ServerMessage, IResponse, IPlayerRelated
    {
        public string PlayerId { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public PlayerIdResponse(string playerId) : base()
        {
            PlayerId = playerId;
        }
    }
}
