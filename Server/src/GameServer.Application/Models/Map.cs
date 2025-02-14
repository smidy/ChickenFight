using System.Collections.Generic;
using GameServer.Shared.Models;

namespace GameServer.Application.Models
{
    public class Map
    {
        public string Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        private readonly Dictionary<string, Player> players;
        private readonly int[] tileData;

        public Map(string id, string name, int width, int height, int[]? tileData = null)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            players = new Dictionary<string, Player>();
            this.tileData = tileData ?? new int[width * height];
        }

        public IReadOnlyCollection<Player> Players => players.Values;
        
        public int[] TileData => tileData;

        public void SetTile(int x, int y, int tileId)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                tileData[y * Width + x] = tileId;
            }
        }

        public int GetTile(int x, int y)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                return tileData[y * Width + x];
            }
            return -1;
        }

        public bool TryAddPlayer(Player player, out Position? startPosition)
        {
            startPosition = null;
            if (players.ContainsKey(player.Id))
                return false;

            // Find a free position for the player
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var position = new Position(x, y);
                    if (!IsPositionOccupied(position))
                    {
                        startPosition = position;
                        players[player.Id] = player;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool RemovePlayer(string playerId)
        {
            return players.Remove(playerId);
        }

        public bool TryMovePlayer(string playerId, Position newPosition)
        {
            if (!players.TryGetValue(playerId, out var player))
                return false;

            if (!newPosition.IsValid(Width, Height))
                return false;

            if (IsPositionOccupied(newPosition))
                return false;

            if (player.Position == null || !player.Position.IsAdjacent(newPosition))
                return false;

            return true;
        }

        private bool IsPositionOccupied(Position position)
        {
            foreach (var player in players.Values)
            {
                if (player.Position?.X == position.X && player.Position?.Y == position.Y)
                    return true;
            }
            return false;
        }
    }
}
