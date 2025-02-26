using Godot;
using System;
using GameServer.Shared.ExternalMessages;
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
        _network.FightChallengeReceived += OnFightChallengeReceived;
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
        foreach(var player in this._gameState.OtherPlayers)
        {
            OnPlayerJoined(player.Key, player.Value);
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
            _network.SendMessage(new InPlayerMove(new ExPosition((int)newPos.X, (int)newPos.Y)));
        }
    }

    private void OnContextMenuItemSelected(long id)
    {
        if (id == 0 && _rightClickedPlayerId != null) // Challenge
        {
            _network.SendMessage(new InFightChallengeSend(_rightClickedPlayerId));
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

        var sprite = new Sprite2D
        {
            Texture = _player.Texture,
            Position = position * 32 + new Vector2(16, 16)
        };
        AddChild(sprite);
        _otherPlayers[playerId] = sprite;
        _otherPlayersTargetPositions[playerId] = position * 32 + new Vector2(16, 16);
        _gameState.AddPlayer(playerId, position);
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

    private void OnFightChallengeReceived(string challengerId)
    {
        UpdateStatusLabel($"Received fight challenge from {challengerId}");
        // Auto-accept for now
        _network.SendMessage(new InFightChallengeAccepted(challengerId));
    }

    private void OnFightStarted(string opponentId)
    {
        UpdateStatusLabel();
        if (_otherPlayers.TryGetValue(opponentId, out var sprite))
        {
            sprite.Modulate = Colors.Red;
        }
        _player.Modulate = Colors.Red;
    }

    private void OnFightEnded(string winnerId, string reason)
    {
        UpdateStatusLabel($"Fight ended. Winner: {winnerId}. Reason: {reason}");
        if (_gameState.OpponentId != null && _otherPlayers.TryGetValue(_gameState.OpponentId, out var sprite))
        {
            sprite.Modulate = Colors.White;
        }
        _player.Modulate = new Color(0, 0.6f, 1);
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
        _network.FightChallengeReceived -= OnFightChallengeReceived;
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
