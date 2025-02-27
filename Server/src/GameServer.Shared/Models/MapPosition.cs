using System;

namespace GameServer.Shared.Models
{
    public record MapPosition(int X, int Y)
    {
        public bool IsAdjacent(MapPosition other)
        {
            var dx = Math.Abs(X - other.X);
            var dy = Math.Abs(Y - other.Y);
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1)||(dx == 1 && dy == 1);
        }

        public bool IsValid(int maxX, int maxY)
        {
            return X >= 0 && X < maxX && Y >= 0 && Y < maxY;
        }
    }
}
