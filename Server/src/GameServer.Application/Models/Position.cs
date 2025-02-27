using GameServer.Shared.Models;

namespace GameServer.Application.Models
{
    public class PositionRef
    {
        public static MapPosition Create(int x, int y) => new MapPosition(x, y);
    }
}
