using Godot;
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
    
    // Card battle state
    public Godot.Collections.Dictionary<string, string> CardSvgData { get; private set; } = new();
    public Godot.Collections.Array<Dictionary> CardsInHand { get; private set; } = new();
    public int PlayerHitPoints { get; private set; } = 50;
    public int PlayerActionPoints { get; private set; } = 0;
    public int PlayerDeckCount { get; private set; } = 0;
    public int PlayerDiscardPileCount { get; private set; } = 0;
    public int OpponentHitPoints { get; private set; } = 50;
    public int OpponentActionPoints { get; private set; } = 0;
    public int OpponentDeckCount { get; private set; } = 0;
    public int OpponentDiscardPileCount { get; private set; } = 0;
    public Godot.Collections.Array<Dictionary> OpponentCardsInHand { get; private set; } = new();
    public Godot.Collections.Array<Dictionary> PlayerStatusEffects { get; private set; } = new();
    public Godot.Collections.Array<Dictionary> OpponentStatusEffects { get; private set; } = new();
    public Dictionary LastPlayedCard { get; private set; } = null;
    public string? CurrentTurnPlayerId { get; private set; }
    public bool IsPlayerTurn => CurrentTurnPlayerId == PlayerId;

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
        
        // Card battle signals
        network.CardImagesReceived += OnCardImagesReceived;
        network.CardDrawn += OnCardDrawn;
        network.TurnStarted += OnTurnStarted;
        network.TurnEnded += OnTurnEnded;
        network.CardPlayCompleted += OnCardPlayCompleted;
        network.CardPlayFailed += OnCardPlayFailed;
        network.EffectApplied += OnEffectApplied;
        network.FightStateUpdated += OnFightStateUpdated;
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
        
        // Reset card battle state
        CardSvgData.Clear();
        CardsInHand.Clear();
        PlayerHitPoints = 50;
        PlayerActionPoints = 0;
        PlayerDeckCount = 0;
        PlayerDiscardPileCount = 0;
        OpponentHitPoints = 50;
        OpponentActionPoints = 0;
        OpponentDeckCount = 0;
        OpponentDiscardPileCount = 0;
        OpponentCardsInHand.Clear();
        CurrentTurnPlayerId = null;
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
        
        // Reset card battle state for new fight
        CardSvgData.Clear();
        CardsInHand.Clear();
        PlayerHitPoints = 50;
        PlayerActionPoints = 0;
        PlayerDeckCount = 0;
        PlayerDiscardPileCount = 0;
        OpponentHitPoints = 50;
        OpponentActionPoints = 0;
        OpponentDeckCount = 0;
        OpponentDiscardPileCount = 0;
        OpponentCardsInHand.Clear();
        CurrentTurnPlayerId = null;
    }

    private void OnFightEnded(string winnerId, string reason)
    {
        // Special handling for disconnection
        if (reason == "Player disconnected" && OpponentId != null)
        {
            // Remove the disconnected player from the PlayersInFight dictionary
            PlayersInFight.Remove(OpponentId);
            
            // Also remove from OtherPlayers if needed
            OtherPlayers.Remove(OpponentId);
        }
        else if (OpponentId != null)
        {
            // Normal cleanup for both players
            PlayersInFight.Remove(PlayerId);
            PlayersInFight.Remove(OpponentId);
        }
        
        // Reset fight state
        CurrentFightId = null;
        OpponentId = null;
        
        // Reset card battle state
        CardSvgData.Clear();
        CardsInHand.Clear();
        PlayerHitPoints = 50;
        PlayerActionPoints = 0;
        PlayerDeckCount = 0;
        PlayerDiscardPileCount = 0;
        OpponentHitPoints = 50;
        OpponentActionPoints = 0;
        OpponentDeckCount = 0;
        OpponentDiscardPileCount = 0;
        OpponentCardsInHand.Clear();
        CurrentTurnPlayerId = null;
    }
    
    // Card battle event handlers
    private void OnCardImagesReceived(Godot.Collections.Dictionary<string, string> cardSvgData)
    {
        foreach (var entry in cardSvgData)
        {
            CardSvgData[entry.Key] = entry.Value;
        }
    }
    
    private void OnCardDrawn(Dictionary cardInfo, string svgData)
    {
        // Only update the SVG data cache, don't modify the hand
        string cardId = cardInfo["Id"].AsString();
        CardSvgData[cardId] = svgData;
        
        // No longer adding cards to hand here - this is now handled by OnFightStateUpdate
    }
    
    private void OnTurnStarted(string activePlayerId)
    {
        CurrentTurnPlayerId = activePlayerId;
        
        // Card handling is now managed by OnFightStateUpdate
    }
    
    private void OnTurnEnded(string playerId)
    {
        if (playerId == PlayerId)
        {
            // Player's turn ended
            CurrentTurnPlayerId = OpponentId;
            
            // Clear player's hand as it's moved to discard pile
            CardsInHand.Clear();
        }
        else
        {
            // Opponent's turn ended
            CurrentTurnPlayerId = PlayerId;
            
            // Clear opponent's hand as it's moved to discard pile
            OpponentCardsInHand.Clear();
        }
    }
    
    private void OnCardPlayCompleted(string playerId, Dictionary playedCard, string effect)
    {
        string cardId = playedCard["Id"].AsString();
        
        // Store the last played card with player ID
        LastPlayedCard = new Dictionary();
        foreach (var key in playedCard.Keys)
        {
            LastPlayedCard[key] = playedCard[key];
        }
        LastPlayedCard["PlayerId"] = playerId;
        
        if (playerId == PlayerId)
        {
            // Remove the card from the player's hand
            for (int i = 0; i < CardsInHand.Count; i++)
            {
                if (CardsInHand[i]["Id"].AsString() == cardId)
                {
                    CardsInHand.RemoveAt(i);
                    break;
                }
            }
        }
        else
        {
            // Remove the card from the opponent's hand
            for (int i = 0; i < OpponentCardsInHand.Count; i++)
            {
                if (OpponentCardsInHand[i]["Id"].AsString() == cardId)
                {
                    OpponentCardsInHand.RemoveAt(i);
                    break;
                }
            }
        }
    }
    
    private void OnCardPlayFailed(string cardId, string error)
    {
        // Handle card play failure (e.g., show error message)
        GD.PrintErr($"Card play failed: {error}");
    }
    
    private void OnEffectApplied(string targetPlayerId, string effectType, int value, string source)
    {
        // Update player stats based on effect
        if (targetPlayerId == PlayerId)
        {
            if (effectType == "Damage")
            {
                PlayerHitPoints = Math.Max(0, PlayerHitPoints - value);
            }
            else if (effectType == "Heal")
            {
                PlayerHitPoints = Math.Min(50, PlayerHitPoints + value);
            }
        }
        else
        {
            if (effectType == "Damage")
            {
                OpponentHitPoints = Math.Max(0, OpponentHitPoints - value);
            }
            else if (effectType == "Heal")
            {
                OpponentHitPoints = Math.Min(50, OpponentHitPoints + value);
            }
        }
    }
    
    private void OnFightStateUpdated(string currentTurnPlayerId, Dictionary playerState, Dictionary opponentState)
    {
        CurrentTurnPlayerId = currentTurnPlayerId;
        
        // Determine which state belongs to the player and which to the opponent
        string playerStateId = playerState["PlayerId"].AsString();
        string opponentStateId = opponentState["PlayerId"].AsString();
        
        // If the player state ID doesn't match the player's ID, swap the states
        if (playerStateId != PlayerId)
        {
            var temp = playerState;
            playerState = opponentState;
            opponentState = temp;
        }
        
        // Update player state
        PlayerHitPoints = playerState["HitPoints"].AsInt32();
        PlayerActionPoints = playerState["ActionPoints"].AsInt32();
        PlayerDeckCount = playerState["DeckCount"].AsInt32();
        // Check if DiscardPileCount exists in the dictionary
        PlayerDiscardPileCount = playerState.ContainsKey("DiscardPileCount") ? 
            playerState["DiscardPileCount"].AsInt32() : 0;
        
        // Update player hand
        CardsInHand.Clear();
        var playerHand = (Godot.Collections.Array)playerState["Hand"];
        foreach (Dictionary card in playerHand)
        {
            CardsInHand.Add(card);
        }
        
        // Update player status effects if available
        PlayerStatusEffects.Clear();
        if (playerState.ContainsKey("StatusEffects"))
        {
            var statusEffects = (Godot.Collections.Array)playerState["StatusEffects"];
            foreach (Dictionary effect in statusEffects)
            {
                PlayerStatusEffects.Add(effect);
            }
        }
        
        // Update opponent state
        OpponentHitPoints = opponentState["HitPoints"].AsInt32();
        OpponentActionPoints = opponentState["ActionPoints"].AsInt32();
        OpponentDeckCount = opponentState["DeckCount"].AsInt32();
        // Check if DiscardPileCount exists in the dictionary
        OpponentDiscardPileCount = opponentState.ContainsKey("DiscardPileCount") ? 
            opponentState["DiscardPileCount"].AsInt32() : 0;
        
        // Update opponent hand
        OpponentCardsInHand.Clear();
        var opponentHand = (Godot.Collections.Array)opponentState["Hand"];
        foreach (Dictionary card in opponentHand)
        {
            OpponentCardsInHand.Add(card);
        }
        
        // Update opponent status effects if available
        OpponentStatusEffects.Clear();
        if (opponentState.ContainsKey("StatusEffects"))
        {
            var statusEffects = (Godot.Collections.Array)opponentState["StatusEffects"];
            foreach (Dictionary effect in statusEffects)
            {
                OpponentStatusEffects.Add(effect);
            }
        }
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
        
        // Card battle signals
        network.CardImagesReceived -= OnCardImagesReceived;
        network.CardDrawn -= OnCardDrawn;
        network.TurnStarted -= OnTurnStarted;
        network.TurnEnded -= OnTurnEnded;
        network.CardPlayCompleted -= OnCardPlayCompleted;
        network.CardPlayFailed -= OnCardPlayFailed;
        network.EffectApplied -= OnEffectApplied;
        network.FightStateUpdated -= OnFightStateUpdated;
    }
}
