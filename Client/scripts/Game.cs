using Godot;
using System;
using GameServer.Shared.Messages.Base;
using GameServer.Shared.Messages.Connection;
using GameServer.Shared.Messages.Map;
using GameServer.Shared.Messages.Movement;
using GameServer.Shared.Messages.Fight;
using GameServer.Shared.Messages.CardBattle;
using GameServer.Shared.Models;

public partial class Game : Node2D
{
    private TileMapLayer _tileMap = null!;
    private Sprite2D _player = null!;
    private NetworkManager _network = null!;
    private GameState _gameState = null!;
    private Camera2D _camera = null!;
    private Label _statusLabel = null!;
    private PopupMenu _contextMenu = null!;

    // Fight state
    private string? _rightClickedPlayerId;
    private CardBattle _cardBattle = null!;
    private bool _cardBattleActive = false;
    private PackedScene _cardBattleScene = null!;

    // Other players
    private Dictionary<string, Sprite2D> _otherPlayers = new();
    private Dictionary<string, Vector2> _otherPlayersTargetPositions = new();

    // Tile IDs in the tileset
    private const int GRASS_TILE = 0;
    private const int PATH_TILE = 1;

    // Movement
    private Vector2 _targetPosition;
    private const float MOVE_SPEED = 10.0f;

    public override void _Ready()
    {
        _tileMap = GetNode<TileMapLayer>("Ground");
        _player = GetNode<Sprite2D>("Player");
        _camera = GetNode<Camera2D>("Camera2D");
        _statusLabel = GetNode<Label>("UILayer/StatusLabel");
        _network = GetNode<NetworkManager>("/root/NetworkManager");
        _gameState = GetNode<GameState>("/root/GameState");
        
        // Load the card battle scene
        _cardBattleScene = GD.Load<PackedScene>("res://scenes/CardBattle.tscn");

        // Connect movement signals
        _network.MoveInitiated += OnMoveInitiated;
        _network.MoveCompleted += OnMoveCompleted;
        _network.MoveFailed += OnMoveFailed;
        _network.PlayerStateUpdated += OnPlayerStateUpdated;
        _network.ConnectionLost += OnConnectionLost;

        // Connect other player signals
        _network.PlayerJoinedMap += OnPlayerJoined;
        _network.PlayerPositionChanged += OnPlayerPositionChanged;
        _network.PlayerLeftMap += OnPlayerLeft;

        // Connect fight signals
        _network.FightStarted += OnFightStarted;
        _network.FightEnded += OnFightEnded;

        SetupTilemap();
        SetupPlayer();
        SetupOtherPlayers();
        SetupUI();
        UpdateStatusLabel();
    }

    private void SetupTilemap()
    {
        if (_gameState.CurrentTilemapData == null)
        {
            GD.PrintErr("No tilemap data available!");
            return;
        }

        // Clear existing tiles
        _tileMap.Clear();

        // Set the tilemap layer 0 with the received tile data
        var data = _gameState.CurrentTilemapData;
        var width = data["Width"].AsInt32();
        var height = data["Height"].AsInt32();
        var tileData = (Godot.Collections.Array)data["TileData"];

        // Set camera limits based on map dimensions
        _camera.LimitLeft = 0;
        _camera.LimitTop = 0;
        _camera.LimitRight = width * 32;
        _camera.LimitBottom = height * 32;
        _camera.LimitSmoothed = true;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tileId = tileData[y * width + x].AsInt32();
                _tileMap.SetCell(new Vector2I(x, y), 0, new Vector2I(tileId, 0));
            }
        }
    }

    private void SetupPlayer()
    {
        // Set initial position with center offset
        _player.Position = _gameState.PlayerPosition * 32 + new Vector2(16, 16); // 32 is tile size, offset by half tile
        _targetPosition = _player.Position;
        
        // Center camera on player
        _camera.Position = _player.Position;
    }

    private void SetupOtherPlayers()
    {
        foreach(var player in this._gameState.OtherPlayerInfo)
        {
            var playerId = player.Key;
            var playerInfo = player.Value;
            var position = (Vector2I)playerInfo["Position"];
            var fightId = playerInfo["FightId"].AsString();
            
            // Create sprite for the player
            var sprite = new Sprite2D
            {
                Texture = _player.Texture,
                Position = position * 32 + new Vector2(16, 16)
            };
            
            // If the player is in a fight, color them red
            if (!string.IsNullOrEmpty(fightId))
            {
                sprite.Modulate = Colors.Red;
                _gameState.PlayersInFight[playerId] = true;
            }
            
            AddChild(sprite);
            _otherPlayers[playerId] = sprite;
            _otherPlayersTargetPositions[playerId] = position * 32 + new Vector2(16, 16);
        }
    }

    private void SetupUI()
    {
        _contextMenu = GetNode<PopupMenu>("UI/ContextMenu");
        _contextMenu.IdPressed += OnContextMenuItemSelected;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_network.IsConnected) return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
        {
            // Convert global mouse position to local coordinates
            var mousePos = GetLocalMousePosition();
            
            // Check if clicked on another player
            foreach (var (playerId, sprite) in _otherPlayers)
            {
                if (sprite.GetRect().HasPoint(mousePos - sprite.Position))
                {
                    _rightClickedPlayerId = playerId;
                    
                    // Only show Challenge option if target player is not in a fight
                    _contextMenu.SetItemDisabled(0, _gameState.PlayersInFight.ContainsKey(playerId));
                    
                    // Show context menu at mouse position
                    _contextMenu.Position = (Vector2I)GetViewport().GetMousePosition();
                    _contextMenu.Popup();
                    return;
                }
            }
        }

        if (_gameState.IsMoving) return;

        Vector2I movement = Vector2I.Zero;

        if (Input.IsActionJustPressed("ui_up"))
            movement = new Vector2I(0, -1);
        else if (Input.IsActionJustPressed("ui_down"))
            movement = new Vector2I(0, 1);
        else if (Input.IsActionJustPressed("ui_left"))
            movement = new Vector2I(-1, 0);
        else if (Input.IsActionJustPressed("ui_right"))
            movement = new Vector2I(1, 0);

        if (movement != Vector2I.Zero)
        {
            // Remove the half-tile offset before calculating grid position
            var currentGridPos = ((_player.Position - new Vector2(16, 16)) / 32).Round();
            var newPos = currentGridPos + movement;
            _network.SendMessage(new ExtPlayerMoveRequest(new MapPosition((int)newPos.X, (int)newPos.Y)));
        }
    }

    private void OnContextMenuItemSelected(long id)
    {
        if (id == 0 && _rightClickedPlayerId != null) // Challenge
        {
            _network.SendMessage(new ExtFightChallengeRequest(_rightClickedPlayerId));
            UpdateStatusLabel($"Challenging player {_rightClickedPlayerId}...");
        }
    }

    public override void _Process(double delta)
    {
        // Smooth player movement
        if (_player.Position.DistanceTo(_targetPosition) > 1)
        {
            _player.Position = _player.Position.Lerp(_targetPosition, (float)delta * MOVE_SPEED);
        }
        else if (_player.Position != _targetPosition)
        {
            _player.Position = _targetPosition;
        }

        // Smooth other players movement
        foreach (var (playerId, sprite) in _otherPlayers)
        {
            if (_otherPlayersTargetPositions.TryGetValue(playerId, out var targetPos))
            {
                if (sprite.Position.DistanceTo(targetPos) > 1)
                {
                    sprite.Position = sprite.Position.Lerp(targetPos, (float)delta * MOVE_SPEED);
                }
                else if (sprite.Position != targetPos)
                {
                    sprite.Position = targetPos;
                }
            }
        }

        // Smooth camera follow
        var targetCameraPos = _player.Position;
        _camera.Position = _camera.Position.Lerp(targetCameraPos, (float)delta * 5.0f);
    }

    private void OnPlayerJoined(string playerId, Vector2I position)
    {
        if (_gameState.PlayerId == playerId) return;

        // Create a dictionary with position and empty fightId
        var playerInfo = new Godot.Collections.Dictionary
        {
            ["Position"] = position,
            ["FightId"] = ""  // Empty string instead of null
        };

        var sprite = new Sprite2D
        {
            Texture = _player.Texture,
            Position = position * 32 + new Vector2(16, 16)
        };
        AddChild(sprite);
        _otherPlayers[playerId] = sprite;
        _otherPlayersTargetPositions[playerId] = position * 32 + new Vector2(16, 16);
        _gameState.AddPlayer(playerId, playerInfo);
    }

    private void OnPlayerPositionChanged(string playerId, Vector2I position)
    {
        if (_gameState.PlayerId == playerId) return;

        if (_otherPlayers.ContainsKey(playerId))
        {
            _otherPlayersTargetPositions[playerId] = position * 32 + new Vector2(16, 16);
            _gameState.UpdatePlayerPosition(playerId, position);
        }
    }

    private void OnPlayerLeft(string playerId)
    {
        if (_otherPlayers.TryGetValue(playerId, out var sprite))
        {
            sprite.QueueFree();
            _otherPlayers.Remove(playerId);
            _otherPlayersTargetPositions.Remove(playerId);
            _gameState.RemovePlayer(playerId);
        }
    }

    private void OnMoveInitiated(Vector2I newPosition)
    {
        _targetPosition = newPosition * 32 + new Vector2(16, 16);
        UpdateStatusLabel();
    }

    private void OnMoveCompleted(Vector2I position)
    {
        _targetPosition = position * 32 + new Vector2(16, 16);
        UpdateStatusLabel();
    }

    private void OnMoveFailed(string error)
    {
        // Return to current position
        _targetPosition = _gameState.PlayerPosition * 32 + new Vector2(16, 16);
        UpdateStatusLabel(error);
    }

    private void OnPlayerStateUpdated(Vector2I position)
    {
        _targetPosition = position * 32 + new Vector2(16, 16);
    }

    private void UpdateStatusLabel(string? error = null)
    {
        if (error != null)
        {
            _statusLabel.Text = $"Error: {error}";
            _statusLabel.Modulate = Colors.Red;
        }
        else if (_gameState.IsMoving)
        {
            _statusLabel.Text = "Moving...";
            _statusLabel.Modulate = Colors.Yellow;
        }
        else if (_gameState.IsInFight)
        {
            _statusLabel.Text = $"In fight with {_gameState.OpponentId}";
            _statusLabel.Modulate = Colors.Orange;
        }
        else
        {
            _statusLabel.Text = "";
            _statusLabel.Modulate = Colors.White;
        }
    }

    private void OnFightStarted(string player1Id, string player2Id)
    {
        UpdateStatusLabel();
        
        // Set both players' sprites to red
        if (_otherPlayers.TryGetValue(player1Id, out var player1Sprite))
        {
            player1Sprite.Modulate = Colors.Red;
        }
        
        if (_otherPlayers.TryGetValue(player2Id, out var player2Sprite))
        {
            player2Sprite.Modulate = Colors.Red;
        }
        
        // Set main player's sprite to red if they're in the fight
        if (player1Id == _gameState.PlayerId || player2Id == _gameState.PlayerId)
        {
            _player.Modulate = Colors.Red;
            
            // Show the card battle UI only if the main player is in the fight
            ShowCardBattleUI();
        }
    }

    private void OnFightEnded(string winnerId, string loserId, string reason)
    {
        // Add specific handling for disconnection
        if (reason == "Player disconnected")
        {
            // Show a more specific message
            UpdateStatusLabel($"Opponent disconnected. You win!");
            
            // Notify the card battle UI about the disconnection if it's active
            if (_cardBattleActive && _cardBattle != null)
            {
                _cardBattle.HandleOpponentDisconnection();
            }
            
            // Remove the disconnected player's sprite if it still exists
            if (_otherPlayers.TryGetValue(loserId, out var sprite))
            {
                // Add a visual effect to show disconnection
                var tween = CreateTween();
                tween.TweenProperty(sprite, "modulate:a", 0.0f, 0.5f);
                tween.TweenCallback(Callable.From(() => {
                    if (_otherPlayers.ContainsKey(loserId))
                    {
                        sprite.QueueFree();
                        _otherPlayers.Remove(loserId);
                        _otherPlayersTargetPositions.Remove(loserId);
                    }
                }));
            }
        }
        else
        {
            // Regular fight end handling
            UpdateStatusLabel($"Fight ended. Winner: {winnerId}. Reason: {reason}");
            
            // Reset winner sprite if they're not the main player
            if (_otherPlayers.TryGetValue(winnerId, out var winnerSprite))
            {
                winnerSprite.Modulate = Colors.White;
            }
            
            // Reset loser sprite if they're not the main player
            if (_otherPlayers.TryGetValue(loserId, out var loserSprite))
            {
                loserSprite.Modulate = Colors.White;
            }
        }
        
        // Reset main player's color if they were in this fight
        if (_gameState.PlayerId == winnerId || _gameState.PlayerId == loserId)
        {
            _player.Modulate = new Color(0, 0.6f, 1);
        }
        
        // Hide the card battle UI
        HideCardBattleUI();
    }
    
    private void ShowCardBattleUI()
    {
        if (_cardBattleActive)
            return;
            
        // Instantiate the card battle scene
        _cardBattle = _cardBattleScene.Instantiate<CardBattle>();
        
        // Add it to the UI layer
        var uiLayer = GetNode("UILayer");
        uiLayer.AddChild(_cardBattle);
        
        // Center the card battle UI in the viewport
        _cardBattle.Position = new Vector2(
            (GetViewport().GetVisibleRect().Size.X - _cardBattle.Size.X * _cardBattle.Scale.X) / 2,
            (GetViewport().GetVisibleRect().Size.Y - _cardBattle.Size.Y * _cardBattle.Scale.Y) / 2
        );
        
        _cardBattleActive = true;
    }
    
    private void HideCardBattleUI()
    {
        if (!_cardBattleActive)
            return;
            
        // Remove the card battle scene
        if (_cardBattle != null)
        {
            _cardBattle.QueueFree();
            _cardBattle = null!;
        }
        
        _cardBattleActive = false;
    }

    private void OnConnectionLost()
    {
        GetTree().ChangeSceneToFile("res://scenes/Title.tscn");
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _network.MoveInitiated -= OnMoveInitiated;
        _network.MoveCompleted -= OnMoveCompleted;
        _network.MoveFailed -= OnMoveFailed;
        _network.PlayerStateUpdated -= OnPlayerStateUpdated;
        _network.ConnectionLost -= OnConnectionLost;
        _network.PlayerJoinedMap -= OnPlayerJoined;
        _network.PlayerPositionChanged -= OnPlayerPositionChanged;
        _network.PlayerLeftMap -= OnPlayerLeft;
        _network.FightStarted -= OnFightStarted;
        _network.FightEnded -= OnFightEnded;

        foreach (var sprite in _otherPlayers.Values)
        {
            sprite.QueueFree();
        }
        _otherPlayers.Clear();
        _otherPlayersTargetPositions.Clear();
    }
}
