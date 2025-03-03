using Godot;
using Godot.Collections;

public partial class CardBattle : Control
{
    // UI elements
    private HBoxContainer _cardContainer = null!;
    private Label _playerStatsLabel = null!;
    private Label _opponentStatsLabel = null!;
    private Label _turnLabel = null!;
    private Button _endTurnButton = null!;
    private Label _statusLabel = null!;
    private Control _effectsContainer = null!;
    private HBoxContainer _opponentCardContainer = null!;
    private HBoxContainer _playerStatusEffectsContainer = null!;
    private HBoxContainer _opponentStatusEffectsContainer = null!;
    
    // Card display
    private System.Collections.Generic.Dictionary<string, TextureRect> _cardNodes = new System.Collections.Generic.Dictionary<string, TextureRect>();
    private System.Collections.Generic.Dictionary<string, TextureRect> _opponentCardNodes = new System.Collections.Generic.Dictionary<string, TextureRect>();
    private System.Collections.Generic.Dictionary<string, Control> _playerStatusEffectNodes = new System.Collections.Generic.Dictionary<string, Control>();
    private System.Collections.Generic.Dictionary<string, Control> _opponentStatusEffectNodes = new System.Collections.Generic.Dictionary<string, Control>();
    private string _selectedCardId = null;
    
    // Last played card tracking
    private Dictionary _lastOpponentPlayedCard = null;
    
    // References
    private NetworkManager _network = null!;
    private GameState _gameState = null!;
    
    // Constants
    private const int CARD_WIDTH = 100;  // Reduced from 150
    private const int CARD_HEIGHT = 140; // Reduced from 210
    private const int CARD_SPACING = 8;  // Slightly reduced from 10
    
    public override void _Ready()
    {
        // Get references
        _network = GetNode<NetworkManager>("/root/NetworkManager");
        _gameState = GetNode<GameState>("/root/GameState");
        
        // Get UI elements
        _cardContainer = GetNode<HBoxContainer>("CardContainer");
        _playerStatsLabel = GetNode<Label>("PlayerStats");
        _opponentStatsLabel = GetNode<Label>("OpponentStats");
        _turnLabel = GetNode<Label>("TurnLabel");
        _endTurnButton = GetNode<Button>("EndTurnButton");
        _statusLabel = GetNode<Label>("StatusLabel");
        _effectsContainer = GetNode<Control>("EffectsContainer");
        _opponentCardContainer = GetNode<HBoxContainer>("OpponentArea/OpponentCardContainer");
        _playerStatusEffectsContainer = GetNode<HBoxContainer>("PlayerStatusEffectsContainer");
        _opponentStatusEffectsContainer = GetNode<HBoxContainer>("OpponentStatusEffectsContainer");
        
        // Connect signals
        _endTurnButton.Pressed += OnEndTurnPressed;
        
        // Connect network signals
        _network.CardImagesReceived += OnCardImagesReceived;
        _network.CardDrawn += OnCardDrawn;
        _network.TurnStarted += OnTurnStarted;
        _network.TurnEnded += OnTurnEnded;
        _network.CardPlayCompleted += OnCardPlayCompleted;
        _network.CardPlayFailed += OnCardPlayFailed;
        _network.EffectApplied += OnEffectApplied;
        _network.FightStateUpdated += OnFightStateUpdated;
        
        // Initial UI update
        UpdateUI();
    }
    
    public override void _ExitTree()
    {
        // Disconnect network signals
        _network.CardImagesReceived -= OnCardImagesReceived;
        _network.CardDrawn -= OnCardDrawn;
        _network.TurnStarted -= OnTurnStarted;
        _network.TurnEnded -= OnTurnEnded;
        _network.CardPlayCompleted -= OnCardPlayCompleted;
        _network.CardPlayFailed -= OnCardPlayFailed;
        _network.EffectApplied -= OnEffectApplied;
        _network.FightStateUpdated -= OnFightStateUpdated;
    }
    
    private void UpdateUI()
    {
        // Update player stats
        _playerStatsLabel.Text = $"Player: {_gameState.PlayerHitPoints} HP | {_gameState.PlayerActionPoints} AP | {_gameState.PlayerDeckCount} cards in deck | {_gameState.PlayerDiscardPileCount} in discard";
        
        // Update opponent stats
        _opponentStatsLabel.Text = $"Opponent: {_gameState.OpponentHitPoints} HP | {_gameState.OpponentActionPoints} AP | {_gameState.OpponentDeckCount} cards in deck | {_gameState.OpponentDiscardPileCount} in discard";
        
        // Update turn label
        if (_gameState.CurrentTurnPlayerId == _gameState.PlayerId)
        {
            _turnLabel.Text = "Your Turn";
            _endTurnButton.Disabled = false;
        }
        else
        {
            _turnLabel.Text = "Opponent's Turn";
            _endTurnButton.Disabled = true;
        }
        
        // Update cards in hand
        UpdateCardDisplay();
        
        // Update status effects display
        UpdateStatusEffectsDisplay();
    }
    
    private void UpdateCardDisplay()
    {
        // Debug log the current hand state
        GD.Print($"Updating card display. Cards in hand: {_gameState.CardsInHand.Count} Cardnodes: {_cardNodes.Count}  CardChildren {_cardContainer.GetChildren().Count}");
        
        // Clear existing cards
        foreach (var node in _cardNodes.Values)
        {
            node.QueueFree();
        }
        _cardNodes.Clear();
        foreach (var thing in _cardContainer.GetChildren())
        {
            _cardContainer.RemoveChild( thing );
        }
        
        // Add cards from hand
        foreach (var cardInfo in _gameState.CardsInHand)
        {
            string cardId = cardInfo["Id"].AsString();
            
            // Check if we have SVG data for this card
            if (!_gameState.CardSvgData.ContainsKey(cardId))
            {
                GD.PrintErr($"Missing SVG data for card {cardId}");
                continue;
            }
                
            // Create card visual
            var cardNode = CreateCardVisual(cardId, cardInfo);
            _cardContainer.AddChild(cardNode);
            _cardNodes[cardId] = cardNode;
            
            GD.Print($"Added card to display: {cardId}");
        }
        
        // Verify the card container has the correct number of children
        GD.Print($"Card container now has {_cardContainer.GetChildCount()} cards");
    }
    
    private TextureRect CreateCardVisual(string cardId, Dictionary cardInfo)
    {
        // Create a TextureRect for the card
        var cardNode = new TextureRect
        {
            CustomMinimumSize = new Vector2(CARD_WIDTH, CARD_HEIGHT),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        
        // Get SVG data and convert to texture
        string svgData = _gameState.CardSvgData[cardId];
        var image = new Image();
        var error = image.LoadSvgFromString(svgData, 1.5f); // Reduced scale factor from 2.0f to 1.5f
        
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to load SVG for card {cardId}: {error}");
        }
        else
        {
            var texture = ImageTexture.CreateFromImage(image);
            cardNode.Texture = texture;
        }
        
        // Add card info as tooltip
        string name = cardInfo["Name"].AsString();
        string description = cardInfo["Description"].AsString();
        int cost = cardInfo["Cost"].AsInt32();
        cardNode.TooltipText = $"{name} (Cost: {cost})\n{description}";
        
        // Make card clickable
        cardNode.GuiInput += (InputEvent @event) => OnCardClicked(@event, cardId, cardInfo);
        
        return cardNode;
    }
    
    private void OnCardClicked(InputEvent @event, string cardId, Dictionary cardInfo)
    {
        // Only handle left mouse button clicks
        if (@event is InputEventMouseButton mouseEvent && 
            mouseEvent.ButtonIndex == MouseButton.Left && 
            mouseEvent.Pressed)
        {
            // Only allow card selection on player's turn
            if (_gameState.CurrentTurnPlayerId != _gameState.PlayerId)
            {
                _statusLabel.Text = "It's not your turn!";
                return;
            }
            
            // Check if player has enough action points
            int cost = cardInfo["Cost"].AsInt32();
            if (_gameState.PlayerActionPoints < cost)
            {
                _statusLabel.Text = $"Not enough action points! Need {cost}, have {_gameState.PlayerActionPoints}";
                return;
            }
            
            // Play the card
            _network.PlayCard(cardId);
            _statusLabel.Text = $"Playing card: {cardInfo["Name"].AsString()}...";
            
            // Visual feedback
            if (_cardNodes.TryGetValue(cardId, out var cardNode))
            {
                // Highlight the selected card
                cardNode.Modulate = new Color(1.5f, 1.5f, 1.5f);
            }
        }
    }
    
    private void OnEndTurnPressed()
    {
        if (_gameState.CurrentTurnPlayerId == _gameState.PlayerId)
        {
            _network.EndTurn();
            _statusLabel.Text = "Ending turn...";
        }
    }
    
    // Network event handlers
    private void OnCardImagesReceived(Godot.Collections.Dictionary<string, string> cardSvgData)
    {
        UpdateCardDisplay();
    }
    
    private void OnCardDrawn(Dictionary cardInfo, string svgData)
    {
        UpdateCardDisplay();
    }
    
    private void OnTurnStarted(string activePlayerId)
    {
        // Force clear the card display at the start of a turn to ensure a clean state
        GD.Print($"Turn started for player {activePlayerId}. Ensuring card display is clean.");
        
        // Clear existing cards immediately
        foreach (var node in _cardNodes.Values)
        {
            node.QueueFree();
        }
        _cardNodes.Clear();
        _cardContainer.GetChildren().Clear();
        
        // Then update the UI which will reflect the current game state
        UpdateUI();
        
        if (activePlayerId == _gameState.PlayerId)
        {
            _statusLabel.Text = "Your turn!";
        }
        else
        {
            _statusLabel.Text = "Opponent's turn.";
        }
    }
    
    private void OnTurnEnded(string playerId)
    {
        // Force clear the card display when a turn ends
        GD.Print($"Turn ended for player {playerId}. Clearing card display.");
        
        // Clear existing cards immediately
        foreach (var node in _cardNodes.Values)
        {
            node.QueueFree();
        }
        _cardNodes.Clear();
        _cardContainer.GetChildren().Clear();
        
        // Then update the UI which will reflect the current game state
        UpdateUI();
    }
    
    private void OnCardPlayCompleted(string playerId, Dictionary playedCard, string effect)
    {
        UpdateUI();
        
        string cardName = playedCard["Name"].AsString();
        if (playerId == _gameState.PlayerId)
        {
            _statusLabel.Text = $"You played {cardName}. {effect}";
        }
        else
        {
            _statusLabel.Text = $"Opponent played {cardName}. {effect}";
            
            // Display the opponent's played card if it's visible
            bool isVisible = playedCard.ContainsKey("IsVisible") ? playedCard["IsVisible"].AsBool() : true;
            if (isVisible)
            {
                DisplayOpponentPlayedCard(playedCard);
            }
        }
        
        // Show effect animation
        ShowEffectAnimation(playerId, effect);
    }
    
    private void OnCardPlayFailed(string cardId, string error)
    {
        _statusLabel.Text = $"Failed to play card: {error}";
        
        // Reset card visual
        if (_cardNodes.TryGetValue(cardId, out var cardNode))
        {
            cardNode.Modulate = Colors.White;
        }
    }
    
    private void OnEffectApplied(string targetPlayerId, string effectType, int value, string source)
    {
        UpdateUI();
        
        // Show effect animation
        ShowEffectAnimation(targetPlayerId, $"{effectType}: {value}");
    }
    
    private void OnFightStateUpdated(string currentTurnPlayerId, Dictionary playerState, Dictionary opponentState)
    {
        // Log the fight state update
        GD.Print($"Fight state updated. Current turn: {currentTurnPlayerId}");
        GD.Print($"Player hand size from server: {((Godot.Collections.Array)playerState["Hand"]).Count}");
        
        // Force clear the card display before updating from the server state
        foreach (var node in _cardNodes.Values)
        {
            node.QueueFree();
        }
        _cardNodes.Clear();
        _cardContainer.GetChildren().Clear();
        
        // Clear opponent card display
        foreach (var node in _opponentCardNodes.Values)
        {
            node.QueueFree();
        }
        _opponentCardNodes.Clear();
        foreach (var child in _opponentCardContainer.GetChildren())
        {
            _opponentCardContainer.RemoveChild(child);
        }
        
        // Update the UI with the latest state from the server
        UpdateUI();
        
        // If there's a last played card by the opponent, display it
        if (_gameState.LastPlayedCard != null && _gameState.LastPlayedCard.ContainsKey("Id"))
        {
            // Only display if it's the opponent's card and it's visible
            string playerId = _gameState.LastPlayedCard.ContainsKey("PlayerId") ? 
                _gameState.LastPlayedCard["PlayerId"].AsString() : "";
            bool isVisible = _gameState.LastPlayedCard.ContainsKey("IsVisible") ? 
                _gameState.LastPlayedCard["IsVisible"].AsBool() : true;
                
            if (playerId != _gameState.PlayerId && isVisible)
            {
                DisplayOpponentPlayedCard(_gameState.LastPlayedCard);
            }
        }
    }
    
    private void ShowEffectAnimation(string targetPlayerId, string effectText)
    {
        // Create a label for the effect
        var effectLabel = new Label
        {
            Text = effectText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(400, targetPlayerId == _gameState.PlayerId ? 220 : 120),
            Theme = new Theme()
        };
        
        // Set font size for better visibility
        effectLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        effectLabel.AddThemeFontSizeOverride("font_size", 16);
        
        // Add to effects container
        _effectsContainer.AddChild(effectLabel);
        
        // Animate and remove
        var tween = CreateTween();
        tween.TweenProperty(effectLabel, "position:y", effectLabel.Position.Y - 40, 1.0f);
        tween.Parallel().TweenProperty(effectLabel, "modulate:a", 0.0f, 1.0f);
        tween.TweenCallback(Callable.From(() => effectLabel.QueueFree()));
    }
    
    // Method to update status effects display for both players
    private void UpdateStatusEffectsDisplay()
    {
        // Clear existing status effect displays
        ClearStatusEffects();
        
        // Update player status effects if available in the fight state update
        if (_gameState.PlayerStatusEffects != null)
        {
            foreach (var effect in _gameState.PlayerStatusEffects)
            {
                DisplayStatusEffect(effect, true);
            }
        }
        
        // Update opponent status effects if available in the fight state update
        if (_gameState.OpponentStatusEffects != null)
        {
            foreach (var effect in _gameState.OpponentStatusEffects)
            {
                DisplayStatusEffect(effect, false);
            }
        }
    }
    
    // Clear all status effect displays
    private void ClearStatusEffects()
    {
        // Clear player status effects
        foreach (var node in _playerStatusEffectNodes.Values)
        {
            node.QueueFree();
        }
        _playerStatusEffectNodes.Clear();
        foreach (var child in _playerStatusEffectsContainer.GetChildren())
        {
            _playerStatusEffectsContainer.RemoveChild(child);
        }
        
        // Clear opponent status effects
        foreach (var node in _opponentStatusEffectNodes.Values)
        {
            node.QueueFree();
        }
        _opponentStatusEffectNodes.Clear();
        foreach (var child in _opponentStatusEffectsContainer.GetChildren())
        {
            _opponentStatusEffectsContainer.RemoveChild(child);
        }
    }
    
    // Display a single status effect
    private void DisplayStatusEffect(Dictionary effectInfo, bool isPlayer)
    {
        string effectId = effectInfo["Id"].AsString();
        string effectName = effectInfo["Name"].AsString();
        string effectDescription = effectInfo["Description"].AsString();
        int duration = effectInfo["Duration"].AsInt32();
        string effectType = effectInfo["Type"].AsString();
        
        // Create a visual representation of the status effect
        var effectNode = new PanelContainer();
        effectNode.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = GetColorForEffectType(effectType),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        });
        
        // Add a label with the effect name and duration
        var label = new Label
        {
            Text = $"{effectName}\n{duration}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(50, 40)
        };
        effectNode.AddChild(label);
        
        // Add tooltip with detailed description
        effectNode.TooltipText = $"{effectName} ({duration} turns)\n{effectDescription}";
        
        // Add to the appropriate container
        if (isPlayer)
        {
            _playerStatusEffectsContainer.AddChild(effectNode);
            _playerStatusEffectNodes[effectId] = effectNode;
        }
        else
        {
            _opponentStatusEffectsContainer.AddChild(effectNode);
            _opponentStatusEffectNodes[effectId] = effectNode;
        }
    }
    
    // Get color based on effect type
    private Color GetColorForEffectType(string effectType)
    {
        return effectType switch
        {
            "DamageOverTime" => new Color(0.8f, 0.2f, 0.2f, 0.7f),    // Red
            "HealOverTime" => new Color(0.2f, 0.8f, 0.2f, 0.7f),      // Green
            "DamageReduction" => new Color(0.2f, 0.2f, 0.8f, 0.7f),   // Blue
            "DamageBoost" => new Color(0.8f, 0.4f, 0.0f, 0.7f),       // Orange
            "DodgeChance" => new Color(0.8f, 0.8f, 0.2f, 0.7f),       // Yellow
            "DamageReflection" => new Color(0.8f, 0.2f, 0.8f, 0.7f),  // Purple
            "CardLock" => new Color(0.5f, 0.5f, 0.5f, 0.7f),          // Gray
            "MaxHealthBoost" => new Color(0.2f, 0.6f, 0.2f, 0.7f),    // Dark Green
            "ActionPointBoost" => new Color(0.2f, 0.6f, 0.8f, 0.7f),  // Cyan
            "EnvironmentEffect" => new Color(0.4f, 0.4f, 0.4f, 0.7f), // Dark Gray
            _ => new Color(0.5f, 0.5f, 0.5f, 0.7f)                    // Default Gray
        };
    }
    
    // Display opponent's played card
    private void DisplayOpponentPlayedCard(Dictionary playedCard)
    {
        // Store the last played card
        _lastOpponentPlayedCard = playedCard;
        
        // Clear existing opponent cards
        foreach (var node in _opponentCardNodes.Values)
        {
            node.QueueFree();
        }
        _opponentCardNodes.Clear();
        foreach (var child in _opponentCardContainer.GetChildren())
        {
            _opponentCardContainer.RemoveChild(child);
        }
        
        // Get card details
        string cardId = playedCard["Id"].AsString();
        
        // Check if we have SVG data for this card
        if (!_gameState.CardSvgData.ContainsKey(cardId))
        {
            GD.PrintErr($"Missing SVG data for opponent's played card {cardId}");
            return;
        }
        
        // Create a smaller version of the card for the opponent area
        var cardNode = new TextureRect
        {
            CustomMinimumSize = new Vector2(CARD_WIDTH * 0.7f, CARD_HEIGHT * 0.7f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        
        // Get SVG data and convert to texture
        string svgData = _gameState.CardSvgData[cardId];
        var image = new Image();
        var error = image.LoadSvgFromString(svgData, 1.2f);
        
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to load SVG for opponent's played card {cardId}: {error}");
        }
        else
        {
            var texture = ImageTexture.CreateFromImage(image);
            cardNode.Texture = texture;
        }
        
        // Add card info as tooltip
        string name = playedCard["Name"].AsString();
        string description = playedCard["Description"].AsString();
        int cost = playedCard["Cost"].AsInt32();
        cardNode.TooltipText = $"{name} (Cost: {cost})\n{description}";
        
        // Add to opponent card container
        _opponentCardContainer.AddChild(cardNode);
        _opponentCardNodes[cardId] = cardNode;
        
        GD.Print($"Displayed opponent's played card: {cardId}");
    }
}
