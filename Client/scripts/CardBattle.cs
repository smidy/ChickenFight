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
    
    // Card display
    private System.Collections.Generic.Dictionary<string, TextureRect> _cardNodes = new System.Collections.Generic.Dictionary<string, TextureRect>();
    private string _selectedCardId = null;
    
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
        _playerStatsLabel.Text = $"Player: {_gameState.PlayerHitPoints} HP | {_gameState.PlayerActionPoints} AP | {_gameState.PlayerDeckCount} cards in deck";
        
        // Update opponent stats
        _opponentStatsLabel.Text = $"Opponent: {_gameState.OpponentHitPoints} HP | {_gameState.OpponentActionPoints} AP | {_gameState.OpponentDeckCount} cards in deck";
        
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
    }
    
    private void UpdateCardDisplay()
    {
        // Clear existing cards
        foreach (var node in _cardNodes.Values)
        {
            node.QueueFree();
        }
        _cardNodes.Clear();
        _cardContainer.GetChildren().Clear();
        
        // Add cards from hand
        foreach (var cardInfo in _gameState.CardsInHand)
        {
            string cardId = cardInfo["Id"].AsString();
            
            // Check if we have SVG data for this card
            if (!_gameState.CardSvgData.ContainsKey(cardId))
                continue;
                
            // Create card visual
            var cardNode = CreateCardVisual(cardId, cardInfo);
            _cardContainer.AddChild(cardNode);
            _cardNodes[cardId] = cardNode;
        }
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
    
    private void OnTurnStarted(string activePlayerId, Godot.Collections.Array drawnCards)
    {
        UpdateUI();
        
        if (activePlayerId == _gameState.PlayerId)
        {
            _statusLabel.Text = "Your turn! Draw cards.";
        }
        else
        {
            _statusLabel.Text = "Opponent's turn.";
        }
    }
    
    private void OnTurnEnded(string playerId)
    {
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
        UpdateUI();
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
}
