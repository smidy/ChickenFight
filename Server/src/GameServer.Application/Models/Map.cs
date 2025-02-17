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
        private readonly Dictionary<string, Position> playerPositions;
        private readonly Dictionary<string, string> playerFightIds;
        private readonly int[] tileData;

        public Map(string id, string name, int width, int height, int[]? tileData = null)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            playerPositions = new Dictionary<string, Position>();
            playerFightIds = new Dictionary<string, string>();
            this.tileData = tileData ?? new int[width * height];
        }

        public IReadOnlyDictionary<string, Position> PlayerPositions => playerPositions;
        
        public int[] TileData => tileData;

        public Position? GetPlayerPosition(string playerId)
        {
            return playerPositions.TryGetValue(playerId, out var position) ? position : null;
        }

        public bool IsPlayerInFight(string playerId)
        {
            return playerFightIds.ContainsKey(playerId);
        }

        public string? GetPlayerFightId(string playerId)
        {
            return playerFightIds.TryGetValue(playerId, out var fightId) ? fightId : null;
        }

        public void SetPlayerFightId(string playerId, string? fightId)
        {
            if (fightId == null)
            {
                playerFightIds.Remove(playerId);
            }
            else
            {
                playerFightIds[playerId] = fightId;
            }
        }

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

        public bool TryAddPlayer(string playerId, out Position? startPosition)
        {
            startPosition = null;
            if (playerPositions.ContainsKey(playerId))
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
                        playerPositions[playerId] = position;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool RemovePlayer(string playerId)
        {
            playerFightIds.Remove(playerId);
            return playerPositions.Remove(playerId);
        }

        public bool TryMovePlayer(string playerId, Position newPosition)
        {
            if (!playerPositions.TryGetValue(playerId, out var currentPosition))
                return false;

            if (!newPosition.IsValid(Width, Height))
                return false;

            if (IsPositionOccupied(newPosition))
                return false;

            if (!currentPosition.IsAdjacent(newPosition))
                return false;

            playerPositions[playerId] = newPosition;
            return true;
        }

        private bool IsPositionOccupied(Position position)
        {
            foreach (var playerPosition in playerPositions.Values)
            {
                if (playerPosition.X == position.X && playerPosition.Y == position.Y)
                    return true;
            }
            return false;
        }
    }
}
