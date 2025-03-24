using GameServer.Shared.Messages.Base;
using GameServer.Shared.Models;

namespace GameServer.Shared.Messages.Movement
{
    /// <summary>
    /// Client request to move player to a new position
    /// </summary>
    public class ExtPlayerMoveRequest : ExtClientMessage, IExtRequest
    {
        public MapPosition NewPosition { get; }

        public ExtPlayerMoveRequest(MapPosition newPosition) : base()
        {
            NewPosition = newPosition;
        }
    }

    /// <summary>
    /// Server notification that move process has started
    /// </summary>
    public class ExtMoveInitiated : ExtServerMessage, IExtNotification
    {
        public MapPosition NewPosition { get; }

        public ExtMoveInitiated(MapPosition newPosition) : base()
        {
            NewPosition = newPosition;
        }
    }

    /// <summary>
    /// Server notification that move has completed successfully
    /// </summary>
    public class ExtMoveCompleted : ExtServerMessage, IExtResponse
    {
        public MapPosition NewPosition { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public ExtMoveCompleted(MapPosition newPosition) : base()
        {
            NewPosition = newPosition;
        }
    }

    /// <summary>
    /// Server notification that move has failed
    /// </summary>
    public class ExtMoveFailed : ExtServerMessage, IExtResponse
    {
        public MapPosition AttemptedPosition { get; }
        public bool Success => false;
        public string ErrorMessage { get; }

        public ExtMoveFailed(MapPosition attemptedPosition, string error) : base()
        {
            AttemptedPosition = attemptedPosition;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Server notification of a player position change
    /// </summary>
    public class ExtPlayerPositionChange : ExtServerMessage, IExtNotification, IExtPlayerRelated
    {
        public string PlayerId { get; }
        public MapPosition? Position { get; }

        public ExtPlayerPositionChange(string playerId, MapPosition? position) : base()
        {
            PlayerId = playerId;
            Position = position;
        }
    }
}
