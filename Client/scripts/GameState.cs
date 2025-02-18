using Godot;
using System;
using Godot.Collections;

public partial class GameState : Node
{
    public static GameState Instance { get; private set; } = null!;
    
    // Current map data
    public Dictionary? CurrentTilemapData { get; set; }
    public string? CurrentMapId { get; set; }
    
    // Player data
    public string? SessionId { get; set; }
    public string? PlayerId { get; set; }
    public Vector2I PlayerPosition { get; set; }
    
    // Other players
    public  Godot.Collections.Dictionary<string, Vector2I> OtherPlayers = new();

    // Pending operations
    public Vector2I? PendingMove { get; private set; }
    public bool IsMoving => PendingMove != null;

    public override void _Ready()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            QueueFree();
        }

        var network = GetNode<NetworkManager>("/root/NetworkManager");
        network.MoveInitiated += OnMoveInitiated;
        network.MoveCompleted += OnMoveCompleted;
        network.MoveFailed += OnMoveFailed;
        network.PlayerStateUpdated += OnPlayerStateUpdated;
    }

    public void Reset()
    {
        CurrentTilemapData = null;
        CurrentMapId = null;
        PlayerPosition = Vector2I.Zero;
        PendingMove = null;
        OtherPlayers.Clear();
    }

    public void AddPlayer(string playerId, Vector2I position)
    {
        if (playerId != SessionId)
        {
            OtherPlayers[playerId] = position;
        }
    }

    public void UpdatePlayerPosition(string playerId, Vector2I position)
    {
        if (playerId != SessionId && OtherPlayers.ContainsKey(playerId))
        {
            OtherPlayers[playerId] = position;
        }
    }

    public void RemovePlayer(string playerId)
    {
        OtherPlayers.Remove(playerId);
    }

    private void OnMoveInitiated(Vector2I newPosition)
    {
        PendingMove = newPosition;
    }

    private void OnMoveCompleted(Vector2I position)
    {
        PlayerPosition = position;
        PendingMove = null;
    }

    private void OnMoveFailed(string error)
    {
        PendingMove = null;
        GD.PrintErr($"Move failed: {error}");
    }

    private void OnPlayerStateUpdated(Vector2I position)
    {
        PlayerPosition = position;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        var network = GetNode<NetworkManager>("/root/NetworkManager");
        network.MoveInitiated -= OnMoveInitiated;
        network.MoveCompleted -= OnMoveCompleted;
        network.MoveFailed -= OnMoveFailed;
        network.PlayerStateUpdated -= OnPlayerStateUpdated;
    }
}
