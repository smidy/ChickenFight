using GameServer.Shared.Models;

namespace GameServer.Application.Models
{
    public class Player
    {
        public string Id { get; }
        public string Name { get; }
        public string? CurrentMapId { get; private set; }
        public Position? Position { get; private set; }

        public Player(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public void JoinMap(string mapId, Position position)
        {
            CurrentMapId = mapId;
            Position = position;
        }

        public void LeaveMap()
        {
            CurrentMapId = null;
            Position = null;
        }

        public void UpdatePosition(Position newPosition)
        {
            Position = newPosition;
        }
    }
}
