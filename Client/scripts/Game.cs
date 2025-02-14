using Godot;
using System;
using GameServer.Shared.Messages;
using GameServer.Shared.Models;

public partial class Game : Node2D
{
    private TileMapLayer _tileMap = null!;
    private Sprite2D _player = null!;
    private NetworkManager _network = null!;
    private GameState _gameState = null!;
    private Camera2D _camera = null!;
    private Label _statusLabel = null!;

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
        _statusLabel = GetNode<Label>("UI/StatusLabel");
        _network = GetNode<NetworkManager>("/root/NetworkManager");
        _gameState = GetNode<GameState>("/root/GameState");

        // Connect movement signals
        _network.MoveInitiated += OnMoveInitiated;
        _network.MoveCompleted += OnMoveCompleted;
        _network.MoveFailed += OnMoveFailed;
        _network.PlayerStateUpdated += OnPlayerStateUpdated;
        _network.ConnectionLost += OnConnectionLost;

        SetupTilemap();
        SetupPlayer();
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
        // Set initial position
        _player.Position = _gameState.PlayerPosition * 32; // 32 is tile size
        _targetPosition = _player.Position;
        
        // Center camera on player
        _camera.Position = _player.Position;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_network.IsConnected || _gameState.IsMoving) return;

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
            var newPos = (_player.Position / 32).Round() + movement;
            _network.SendMessage(new ExtMove(new Position((int)newPos.X, (int)newPos.Y)));
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

        // Smooth camera follow
        var targetCameraPos = _player.Position;
        _camera.Position = _camera.Position.Lerp(targetCameraPos, (float)delta * 5.0f);
    }

    private void OnMoveInitiated(Vector2I newPosition)
    {
        _targetPosition = newPosition * 32;
        UpdateStatusLabel();
    }

    private void OnMoveCompleted(Vector2I position)
    {
        _targetPosition = position * 32;
        UpdateStatusLabel();
    }

    private void OnMoveFailed(string error)
    {
        // Return to current position
        _targetPosition = _gameState.PlayerPosition * 32;
        UpdateStatusLabel(error);
    }

    private void OnPlayerStateUpdated(Vector2I position)
    {
        _targetPosition = position * 32;
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
        else
        {
            _statusLabel.Text = "";
            _statusLabel.Modulate = Colors.White;
        }
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
    }
}
