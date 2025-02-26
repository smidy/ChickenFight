using Godot;
using System;
using System.Threading.Tasks;
using Godot.Collections;
using GameServer.Shared;
using GameServer.Shared.Models;
using GameServer.Shared.ExternalMessages;

public partial class NetworkManager : Node
{
    private WebSocketPeer? _webSocket;
    public bool IsConnected => _webSocket?.GetReadyState() == WebSocketPeer.State.Open;
    
    public static NetworkManager Instance { get; private set; } = null!;

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
    }

    public override void _Process(double delta)
    {
        if (_webSocket == null) return;

        var previousState = _webSocket.GetReadyState();
        _webSocket.Poll();
        var currentState = _webSocket.GetReadyState();
        
        // Check if connection was lost
        if (previousState == WebSocketPeer.State.Open && currentState != WebSocketPeer.State.Open)
        {
            EmitSignal(SignalName.ConnectionLost);
            GD.Print("WebSocket connection lost");
        }
        
        while (_webSocket.GetAvailablePacketCount() > 0)
        {
            var packet = _webSocket.GetPacket();
            var message = System.Text.Encoding.UTF8.GetString(packet);
            HandleMessage(message);
        }
    }

    public async Task Connect(string url = "ws://127.0.0.1:8080")
    {
        _webSocket = new WebSocketPeer();
        var error = _webSocket.ConnectToUrl(url);
        
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to connect to WebSocket server: {error}");
            return;
        }

        // Wait for connection
        while (_webSocket.GetReadyState() == WebSocketPeer.State.Connecting)
        {
            _webSocket.Poll();
            await Task.Delay(100);
        }

        if (_webSocket.GetReadyState() != WebSocketPeer.State.Open)
        {
            GD.PrintErr("Failed to establish WebSocket connection");
            return;
        }

        GD.Print("Connected to WebSocket server");
    }

    public void SendMessage<T>(T message) where T : FromClientMessage
    {
        if (!IsConnected) return;

        // Type is automatically set by BaseMessage constructor
        var json = JsonConfig.Serialize(message);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        _webSocket.Send(bytes);
    }

    private void HandleMessage(string json)
    {
        try
        {
            var message = JsonConfig.Deserialize<ToClientMessage>(json);
            if (message == null) return;

            // Re-deserialize with concrete type based on Type property
            var typedMessage = JsonConfig.Deserialize(json);
            if (typedMessage == null) return;

            switch (typedMessage)
            {
                case OutConnectionConfirmed connectionConfirmed:
                    EmitSignal(SignalName.ConnectionConfirmed, connectionConfirmed.PlayerId);
                    break;

                case RequestMapListResponse mapListResponse:
                    var mapsArray = new Godot.Collections.Array();
                    foreach (var map in mapListResponse.Maps)
                    {
                        var mapDict = new Dictionary
                        {
                            ["Id"] = map.Id,
                            ["Name"] = map.Name,
                            ["Width"] = map.Width,
                            ["Height"] = map.Height,
                            ["PlayerCount"] = map.PlayerCount
                        };
                        mapsArray.Add(mapDict);
                    }
                    EmitSignal(SignalName.MapListReceived, mapsArray);
                    break;

                // Map join messages
                case OutJoinMapInitiated msg:
                    EmitSignal(SignalName.JoinMapInitiated, msg.MapId);
                    break;
                case OutJoinMapCompleted msg:
                    var tilemapDict = new Dictionary
                    {
                        ["Width"] = msg.TilemapData.Width,
                        ["Height"] = msg.TilemapData.Height,
                        ["TileData"] = new Godot.Collections.Array<int>(msg.TilemapData.TileData)
                    };
                    var playerPositions = new Godot.Collections.Dictionary<string, Vector2I>(
                        msg.PlayerPositions.Select( x => KeyValuePair.Create(x.Key, new Vector2I(x.Value.X, x.Value.Y))
                        ).ToDictionary());

                    EmitSignal(SignalName.JoinMapCompleted, msg.PlayerId, new Vector2I(msg.Position.X, msg.Position.Y), tilemapDict, playerPositions);
                    break;
                case OutJoinMapFailed msg:
                    EmitSignal(SignalName.JoinMapFailed, msg.Error);
                    break;

                // Movement messages
                case OutMoveInitiated msg:
                    EmitSignal(SignalName.MoveInitiated, new Vector2I(msg.NewPosition.X, msg.NewPosition.Y));
                    break;
                case OutMoveCompleted msg:
                    EmitSignal(SignalName.MoveCompleted, new Vector2I(msg.NewPosition.X, msg.NewPosition.Y));
                    break;
                case OutMoveFailed msg:
                    EmitSignal(SignalName.MoveFailed, msg.Error);
                    break;

                // Player state messages
                case OutPlayerInfo playerInfo:
                    if (playerInfo.State != null)
                    {
                        EmitSignal(SignalName.PlayerStateUpdated, 
                            new Vector2I(playerInfo.State.Position.X, playerInfo.State.Position.Y));
                    }
                    break;

                case OutPlayerJoinedMap msg:
                    EmitSignal(SignalName.PlayerJoinedMap, msg.PlayerId, 
                        new Vector2I(msg.Position.X, msg.Position.Y));
                    break;

                case OutPlayerPositionChange msg:
                    EmitSignal(SignalName.PlayerPositionChanged, msg.PlayerId,
                        new Vector2I(msg.Position.X, msg.Position.Y));
                    break;

                case OutPlayerLeftMap msg:
                    EmitSignal(SignalName.PlayerLeftMap, msg.PlayerId);
                    break;

                // Fight messages
                case OutFightChallengeReceived msg:
                    EmitSignal(SignalName.FightChallengeReceived, msg.ChallengerId);
                    break;
                case OutFightStarted msg:
                    EmitSignal(SignalName.FightStarted, msg.OpponentId);
                    break;
                case OutFightEnded msg:
                    EmitSignal(SignalName.FightEnded, msg.WinnerId, msg.Reason);
                    break;

                default:
                    GD.PrintErr($"Unknown message type: {message.GetType().Name}");
                    break;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error handling message: {e.Message}");
        }
    }

    [Signal]
    public delegate void ConnectionConfirmedEventHandler(string playerId);

    [Signal]
    public delegate void MapListReceivedEventHandler(Godot.Collections.Array maps);

    // Map join signals
    [Signal]
    public delegate void JoinMapInitiatedEventHandler(string mapId);
    [Signal]
    public delegate void JoinMapCompletedEventHandler(string playerId, Vector2I playerPosition, Dictionary tilemapData, Godot.Collections.Dictionary<string, Vector2I> playerPositions);
    [Signal]
    public delegate void JoinMapFailedEventHandler(string error);

    // Movement signals
    [Signal]
    public delegate void MoveInitiatedEventHandler(Vector2I newPosition);
    [Signal]
    public delegate void MoveCompletedEventHandler(Vector2I position);
    [Signal]
    public delegate void MoveFailedEventHandler(string error);

    // Player state signals
    [Signal]
    public delegate void PlayerStateUpdatedEventHandler(Vector2I position);

    // Other player signals
    [Signal]
    public delegate void PlayerJoinedMapEventHandler(string playerId, Vector2I position);
    [Signal]
    public delegate void PlayerPositionChangedEventHandler(string playerId, Vector2I position);
    [Signal]
    public delegate void PlayerLeftMapEventHandler(string playerId);

    [Signal]
    public delegate void ConnectionLostEventHandler();

    // Fight signals
    [Signal]
    public delegate void FightChallengeReceivedEventHandler(string challengerId);
    [Signal]
    public delegate void FightStartedEventHandler(string opponentId);
    [Signal]
    public delegate void FightEndedEventHandler(string winnerId, string reason);

    public void Disconnect()
    {
        if (_webSocket != null)
        {
            _webSocket.Close();
            _webSocket = null;
        }
    }
}
