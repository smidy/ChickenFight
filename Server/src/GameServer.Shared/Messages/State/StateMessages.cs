using GameServer.Shared.Messages.Base;
using GameServer.Shared.Models;

namespace GameServer.Shared.Messages.State
{
    /// <summary>
    /// Information about a player's state
    /// </summary>
    public class PlayerState
    {
        public string Id { get; }
        public string Name { get; }
        public MapPosition Position { get; }
        public string? FightId { get; }

        public PlayerState(string id, string name, MapPosition position, string? fightId = null)
        {
            Id = id;
            Name = name;
            Position = position;
            FightId = fightId;
        }
    }

    /// <summary>
    /// Server notification with player information
    /// </summary>
    public class ExtPlayerInfo : ExtServerMessage, IExtNotification
    {
        public PlayerState? State { get; }

        public ExtPlayerInfo(PlayerState? state) : base()
        {
            State = state;
        }
    }
}
