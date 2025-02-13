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
    public Vector2I PlayerPosition { get; set; }
    
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
