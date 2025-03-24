namespace GameServer.Shared.Messages.Base
{
    /// <summary>
    /// Base class for all messages with a Type property that contains the derived class name
    /// </summary>
    public abstract class ExtMessageBase
    {
        /// <summary>
        /// The type name of the derived message class
        /// </summary>
        public string MessageType { get; }

        /// <summary>
        /// Base constructor that initializes the Type property with the derived class name
        /// </summary>
        protected ExtMessageBase()
        {
            MessageType = GetType().Name;
        }
    }

    /// <summary>
    /// Base class for all messages sent from client to server
    /// </summary>
    public abstract class ExtClientMessage : ExtMessageBase
    {
        protected ExtClientMessage() : base() { }
    }

    /// <summary>
    /// Base class for all messages sent from server to client
    /// </summary>
    public abstract class ExtServerMessage : ExtMessageBase
    {
        protected ExtServerMessage() : base() { }
    }
}
