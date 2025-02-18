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
    public string? PlayerId { get; set; }
    public Vector2I PlayerPosition { get; set; }
    
    // Other players
    public Godot.Collections.Dictionary<string, Vector2I> OtherPlayers = new();
    public Godot.Collections.Dictionary<string, bool> PlayersInFight = new();

    // Fight state
    public string? CurrentFightId { get; private set; }
    public string? OpponentId { get; private set; }
    public bool IsInFight => CurrentFightId != null;

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
        network.FightStarted += OnFightStarted;
        network.FightEnded += OnFightEnded;
    }

    public void Reset()
    {
        CurrentTilemapData = null;
        CurrentMapId = null;
        PlayerPosition = Vector2I.Zero;
        PendingMove = null;
        OtherPlayers.Clear();
        PlayersInFight.Clear();
        CurrentFightId = null;
        OpponentId = null;
    }

    public void AddPlayer(string playerId, Vector2I position)
    {
        if (playerId != PlayerId)
        {
            OtherPlayers[playerId] = position;
        }
    }

    public void UpdatePlayerPosition(string playerId, Vector2I position)
    {
        if (playerId != PlayerId && OtherPlayers.ContainsKey(playerId))
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

    private void OnFightStarted(string opponentId)
    {
        CurrentFightId = $"fight_{PlayerId}_{opponentId}";
        OpponentId = opponentId;
        PlayersInFight[PlayerId] = true;
        PlayersInFight[opponentId] = true;
    }

    private void OnFightEnded(string winnerId, string reason)
    {
        if (OpponentId != null)
        {
            PlayersInFight.Remove(PlayerId);
            PlayersInFight.Remove(OpponentId);
        }
        CurrentFightId = null;
        OpponentId = null;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        var network = GetNode<NetworkManager>("/root/NetworkManager");
        network.MoveInitiated -= OnMoveInitiated;
        network.MoveCompleted -= OnMoveCompleted;
        network.MoveFailed -= OnMoveFailed;
        network.PlayerStateUpdated -= OnPlayerStateUpdated;
        network.FightStarted -= OnFightStarted;
        network.FightEnded -= OnFightEnded;
    }
}
