using GameServer.Shared.Messages.Base;

namespace GameServer.Shared.Messages.Connection
{
    /// <summary>
    /// Client request for connection confirmation
    /// </summary>
    public class ExtPlayerIdRequest : ExtClientMessage, IExtRequest
    {
        public ExtPlayerIdRequest() : base() { }
    }

    /// <summary>
    /// Server response with connection confirmation
    /// </summary>
    public class ExtPlayerIdResponse : ExtServerMessage, IExtResponse, IExtPlayerRelated
    {
        public string PlayerId { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public ExtPlayerIdResponse(string playerId) : base()
        {
            PlayerId = playerId;
        }
    }
}
