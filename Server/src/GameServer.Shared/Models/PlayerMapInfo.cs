namespace GameServer.Shared.Models
{
    /// <summary>
    /// Contains information about a player on a map, including position and fight status
    /// </summary>
    public class PlayerMapInfo
    {
        public MapPosition Position { get; set; }
        public string? FightId { get; }
        
        public PlayerMapInfo(MapPosition position, string? fightId = null)
        {
            Position = position;
            FightId = fightId;
        }
    }
}
