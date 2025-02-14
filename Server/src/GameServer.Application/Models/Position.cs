using GameServer.Shared.Models;

namespace GameServer.Application.Models
{
    public class PositionRef
    {
        public static Position Create(int x, int y) => new Position(x, y);
    }
}
