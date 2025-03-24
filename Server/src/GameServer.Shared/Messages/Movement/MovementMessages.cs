using GameServer.Shared.Messages.Base;
using GameServer.Shared.Models;

namespace GameServer.Shared.Messages.Movement
{
    /// <summary>
    /// Client request to move player to a new position
    /// </summary>
    public class PlayerMoveRequest : ClientMessage, IRequest
    {
        public MapPosition NewPosition { get; }

        public PlayerMoveRequest(MapPosition newPosition) : base()
        {
            NewPosition = newPosition;
        }
    }

    /// <summary>
    /// Server notification that move process has started
    /// </summary>
    public class MoveInitiated : ServerMessage, INotification
    {
        public MapPosition NewPosition { get; }

        public MoveInitiated(MapPosition newPosition) : base()
        {
            NewPosition = newPosition;
        }
    }

    /// <summary>
    /// Server notification that move has completed successfully
    /// </summary>
    public class MoveCompleted : ServerMessage, IResponse
    {
        public MapPosition NewPosition { get; }
        public bool Success => true;
        public string ErrorMessage => string.Empty;

        public MoveCompleted(MapPosition newPosition) : base()
        {
            NewPosition = newPosition;
        }
    }

    /// <summary>
    /// Server notification that move has failed
    /// </summary>
    public class MoveFailed : ServerMessage, IResponse
    {
        public MapPosition AttemptedPosition { get; }
        public bool Success => false;
        public string ErrorMessage { get; }

        public MoveFailed(MapPosition attemptedPosition, string error) : base()
        {
            AttemptedPosition = attemptedPosition;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Server notification of a player position change
    /// </summary>
    public class PlayerPositionChange : ServerMessage, INotification, IPlayerRelated
    {
        public string PlayerId { get; }
        public MapPosition? Position { get; }

        public PlayerPositionChange(string playerId, MapPosition? position) : base()
        {
            PlayerId = playerId;
            Position = position;
        }
    }
}
