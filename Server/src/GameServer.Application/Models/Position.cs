using GameServer.Shared.Models;

namespace GameServer.Application.Models
{
    public class PositionRef
    {
        public static ExPosition Create(int x, int y) => new ExPosition(x, y);
    }
}
