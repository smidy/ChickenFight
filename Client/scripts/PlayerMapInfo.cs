using Godot;

/// <summary>
/// Contains information about a player on a map, including position and fight status
/// </summary>
public class PlayerMapInfo
{
    public Vector2I Position { get; }
    public string? FightId { get; }
    
    public PlayerMapInfo(Vector2I position, string? fightId = null)
    {
        Position = position;
        FightId = fightId;
    }
}
