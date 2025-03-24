using Godot;
using System;
using System.Threading.Tasks;
using Godot.Collections;
using GameServer.Shared;
using GameServer.Shared.Models;
using GameServer.Shared.Messages.Base;
using GameServer.Shared.Messages.Connection;
using GameServer.Shared.Messages.Map;
using GameServer.Shared.Messages.Movement;
using GameServer.Shared.Messages.Fight;
using GameServer.Shared.Messages.CardBattle;
using GameServer.Shared.Messages.State;

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
        
        // Request connection confirmation
        RequestConnection();
    }
    
    public void RequestConnection()
    {
        SendMessage(new ExtPlayerIdRequest());
    }

    public void SendMessage<T>(T message) where T : ExtClientMessage
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
            var message = JsonConfig.Deserialize<ExtServerMessage>(json);
            if (message == null) return;

            // Re-deserialize with concrete type based on Type property
            var typedMessage = JsonConfig.Deserialize(json);
            if (typedMessage == null) return;

            switch (typedMessage)
            {
                case ExtPlayerIdResponse connectionResponse:
                    EmitSignal(SignalName.ConnectionConfirmed, connectionResponse.PlayerId);
                    break;

                case ExtMapListResponse mapListResponse:
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
                case ExtJoinMapInitiated msg:
                    EmitSignal(SignalName.JoinMapInitiated, msg.MapId);
                    break;
                case ExtJoinMapCompleted msg:
                    var tilemapDict = new Dictionary
                    {
                        ["Width"] = msg.TilemapData.Width,
                        ["Height"] = msg.TilemapData.Height,
                        ["TileData"] = new Godot.Collections.Array<int>(msg.TilemapData.TileData)
                    };
                    
                    // Create a dictionary of player info including positions and fight IDs
                    var playerInfo = new Godot.Collections.Dictionary<string, Dictionary>();
                    foreach (var kvp in msg.PlayerInfo)
                    {
                        var infoDict = new Dictionary
                        {
                            ["Position"] = new Vector2I(kvp.Value.Position.X, kvp.Value.Position.Y),
                            ["FightId"] = kvp.Value.FightId
                        };
                        playerInfo[kvp.Key] = infoDict;
                    }

                    EmitSignal(SignalName.JoinMapCompleted, msg.PlayerId, new Vector2I(msg.Position.X, msg.Position.Y), tilemapDict, playerInfo);
                    break;
                case ExtJoinMapFailed msg:
                    EmitSignal(SignalName.JoinMapFailed, msg.ErrorMessage);
                    break;

                // Movement messages
                case ExtMoveInitiated msg:
                    EmitSignal(SignalName.MoveInitiated, new Vector2I(msg.NewPosition.X, msg.NewPosition.Y));
                    break;
                case ExtMoveCompleted msg:
                    EmitSignal(SignalName.MoveCompleted, new Vector2I(msg.NewPosition.X, msg.NewPosition.Y));
                    break;
                case ExtMoveFailed msg:
                    EmitSignal(SignalName.MoveFailed, msg.ErrorMessage);
                    break;

                // Player state messages
                case ExtPlayerInfo pInfo:
                    if (pInfo.State != null)
                    {
                        EmitSignal(SignalName.PlayerStateUpdated, 
                            new Vector2I(pInfo.State.Position.X, pInfo.State.Position.Y));
                    }
                    break;

                case ExtPlayerJoinedMap msg:
                    EmitSignal(SignalName.PlayerJoinedMap, msg.PlayerId, 
                        new Vector2I(msg.Position.X, msg.Position.Y));
                    break;

                case ExtPlayerPositionChange msg:
                    EmitSignal(SignalName.PlayerPositionChanged, msg.PlayerId,
                        new Vector2I(msg.Position.X, msg.Position.Y));
                    break;

                case ExtPlayerLeftMap msg:
                    EmitSignal(SignalName.PlayerLeftMap, msg.PlayerId);
                    break;

                // Fight messages
                case ExtFightChallengeReceived msg:
                    EmitSignal(SignalName.FightChallengeReceived, msg.ChallengerId);
                    break;
                case ExtFightStarted msg:
                    EmitSignal(SignalName.FightStarted, msg.Player1Id, msg.Player2Id);
                    break;
                case ExtFightEnded msg:
                    EmitSignal(SignalName.FightEnded, msg.WinnerId, msg.LoserId, msg.Reason);
                    break;
                    
                // Card battle messages
                case ExtCardImages msg:
                    EmitSignal(SignalName.CardImagesReceived, new Godot.Collections.Dictionary<string, string>(msg.CardSvgData));
                    break;
                    
                case ExtCardDrawn msg:
                    var drawnCardDict = new Dictionary
                    {
                        ["Id"] = msg.CardInfo.Id,
                        ["Name"] = msg.CardInfo.Name,
                        ["Description"] = msg.CardInfo.Description,
                        ["Cost"] = msg.CardInfo.Cost
                    };
                    EmitSignal(SignalName.CardDrawn, drawnCardDict, msg.SvgData);
                    break;
                    
                case ExtTurnStarted msg:
                    EmitSignal(SignalName.TurnStarted, msg.ActivePlayerId);
                    break;
                    
                case ExtTurnEnded msg:
                    EmitSignal(SignalName.TurnEnded, msg.PlayerId);
                    break;
                    
                case ExtCardPlayCompleted msg:
                    var playedCardDict = new Dictionary
                    {
                        ["Id"] = msg.PlayedCard.Id,
                        ["Name"] = msg.PlayedCard.Name,
                        ["Description"] = msg.PlayedCard.Description,
                        ["Cost"] = msg.PlayedCard.Cost,
                        ["IsVisible"] = msg.IsVisible
                    };
                    EmitSignal(SignalName.CardPlayCompleted, msg.PlayerId, playedCardDict, msg.Effect);
                    break;
                    
                case ExtCardPlayFailed msg:
                    EmitSignal(SignalName.CardPlayFailed, msg.CardId, msg.ErrorMessage);
                    break;
                    
                case ExtEffectApplied msg:
                    EmitSignal(SignalName.EffectApplied, msg.TargetPlayerId, msg.EffectType, msg.Value, msg.Source);
                    break;
                    
                case ExtFightStateUpdate msg:
                    var playerStateDict = new Dictionary
                    {
                        ["PlayerId"] = msg.PlayerState.PlayerId,
                        ["HitPoints"] = msg.PlayerState.HitPoints,
                        ["ActionPoints"] = msg.PlayerState.ActionPoints,
                        ["DeckCount"] = msg.PlayerState.DeckCount
                    };
                    
                    var playerHandArray = new Godot.Collections.Array();
                    foreach (var card in msg.PlayerState.Hand)
                    {
                        var playerCardDict = new Dictionary
                        {
                            ["Id"] = card.Id,
                            ["Name"] = card.Name,
                            ["Description"] = card.Description,
                            ["Cost"] = card.Cost
                        };
                        playerHandArray.Add(playerCardDict);
                    }
                    playerStateDict["Hand"] = playerHandArray;
                    
                    // Add player status effects if available
                    if (msg.PlayerState.StatusEffects != null && msg.PlayerState.StatusEffects.Count > 0)
                    {
                        var playerStatusEffectsArray = new Godot.Collections.Array();
                        foreach (var effect in msg.PlayerState.StatusEffects)
                        {
                            var effectDict = new Dictionary
                            {
                                ["Id"] = effect.Id,
                                ["Name"] = effect.Name,
                                ["Description"] = effect.Description,
                                ["Duration"] = effect.Duration,
                                ["Type"] = effect.Type,
                                ["Magnitude"] = effect.Magnitude
                            };
                            playerStatusEffectsArray.Add(effectDict);
                        }
                        playerStateDict["StatusEffects"] = playerStatusEffectsArray;
                    }
                    
                    var opponentStateDict = new Dictionary
                    {
                        ["PlayerId"] = msg.OpponentState.PlayerId,
                        ["HitPoints"] = msg.OpponentState.HitPoints,
                        ["ActionPoints"] = msg.OpponentState.ActionPoints,
                        ["DeckCount"] = msg.OpponentState.DeckCount
                    };
                    
                    var opponentHandArray = new Godot.Collections.Array();
                    foreach (var card in msg.OpponentState.Hand)
                    {
                        var opponentCardDict = new Dictionary
                        {
                            ["Id"] = card.Id,
                            ["Name"] = card.Name,
                            ["Description"] = card.Description,
                            ["Cost"] = card.Cost
                        };
                        opponentHandArray.Add(opponentCardDict);
                    }
                    opponentStateDict["Hand"] = opponentHandArray;
                    
                    // Add opponent status effects if available
                    if (msg.OpponentState.StatusEffects != null && msg.OpponentState.StatusEffects.Count > 0)
                    {
                        var opponentStatusEffectsArray = new Godot.Collections.Array();
                        foreach (var effect in msg.OpponentState.StatusEffects)
                        {
                            var effectDict = new Dictionary
                            {
                                ["Id"] = effect.Id,
                                ["Name"] = effect.Name,
                                ["Description"] = effect.Description,
                                ["Duration"] = effect.Duration,
                                ["Type"] = effect.Type,
                                ["Magnitude"] = effect.Magnitude
                            };
                            opponentStatusEffectsArray.Add(effectDict);
                        }
                        opponentStateDict["StatusEffects"] = opponentStatusEffectsArray;
                    }
                    
                    EmitSignal(SignalName.FightStateUpdated, msg.CurrentTurnPlayerId, playerStateDict, opponentStateDict);
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
    public delegate void JoinMapCompletedEventHandler(string playerId, Vector2I playerPosition, Dictionary tilemapData, Godot.Collections.Dictionary<string, Dictionary> playerInfo);
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
    public delegate void FightStartedEventHandler(string player1Id, string player2Id);
    [Signal]
    public delegate void FightEndedEventHandler(string winnerId, string loserId, string reason);
    
    // Card battle signals
    [Signal]
    public delegate void CardImagesReceivedEventHandler(Godot.Collections.Dictionary<string, string> cardSvgData);
    [Signal]
    public delegate void CardDrawnEventHandler(Dictionary cardInfo, string svgData);
    [Signal]
    public delegate void TurnStartedEventHandler(string activePlayerId);
    [Signal]
    public delegate void TurnEndedEventHandler(string playerId);
    [Signal]
    public delegate void CardPlayCompletedEventHandler(string playerId, Dictionary playedCard, string effect);
    [Signal]
    public delegate void CardPlayFailedEventHandler(string cardId, string error);
    [Signal]
    public delegate void EffectAppliedEventHandler(string targetPlayerId, string effectType, int value, string source);
    [Signal]
    public delegate void FightStateUpdatedEventHandler(string currentTurnPlayerId, Dictionary playerState, Dictionary opponentState);

    public void Disconnect()
    {
        if (_webSocket != null)
        {
            _webSocket.Close();
            _webSocket = null;
        }
    }
    
    // Card battle methods
    public void PlayCard(string cardId)
    {
        SendMessage(new ExtPlayCardRequest(cardId));
    }
    
    public void EndTurn()
    {
        SendMessage(new ExtEndTurnRequest());
    }
}
