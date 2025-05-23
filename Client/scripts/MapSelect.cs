using Godot;
using System;
using Godot.Collections;
using GameServer.Shared.Messages.Base;
using GameServer.Shared.Messages.Connection;
using GameServer.Shared.Messages.Map;

public partial class MapSelect : Node2D
{
    private ItemList _mapList = null!;
    private Label _statusLabel = null!;
    private NetworkManager _network = null!;
    private Button _refreshButton = null!;

    public override void _Ready()
    {
        _mapList = GetNode<ItemList>("UI/MapList");
        _statusLabel = GetNode<Label>("UI/StatusLabel");
        _refreshButton = GetNode<Button>("UI/RefreshButton");
        _network = GetNode<NetworkManager>("/root/NetworkManager");

        // Connect signals
        _network.MapListReceived += OnMapListReceived;
        _network.JoinMapInitiated += OnJoinMapInitiated;
        _network.JoinMapCompleted += OnJoinMapCompleted;
        _network.JoinMapFailed += OnJoinMapFailed;
        _network.ConnectionLost += OnConnectionLost;
        _mapList.ItemSelected += OnMapSelected;
        _refreshButton.Pressed += RequestMapList;

        // Request initial map list
        RequestMapList();
    }

    private void RequestMapList()
    {
        _statusLabel.Text = "Fetching maps...";
        _mapList.Clear();
        _network.SendMessage(new ExtMapListRequest());
    }

    private void OnMapListReceived(Godot.Collections.Array maps)
    {
        _mapList.Clear();
        foreach (Dictionary map in maps)
        {
            var id = (string)map["Id"];
            var name = (string)map["Name"];
            var playerCount = map["PlayerCount"].AsInt32();
            
            _mapList.AddItem($"{name} ({playerCount} players)");
            _mapList.SetItemMetadata(_mapList.ItemCount - 1, id);
        }
        
        _statusLabel.Text = "Select a map to join";
    }

    private void OnMapSelected(long index)
    {
        var mapId = (string)_mapList.GetItemMetadata((int)index);
        _statusLabel.Text = "Requesting to join map...";
        _network.SendMessage(new ExtJoinMapRequest(mapId));
    }

    private void OnJoinMapInitiated(string mapId)
    {
        _statusLabel.Text = "Joining map...";
        _refreshButton.Disabled = true;
    }

    private void OnJoinMapCompleted(string playerId, Vector2I playerPosition, Dictionary tilemapData, Godot.Collections.Dictionary<string, Dictionary> playerInfo)
    {
        // Store tilemap data for the game scene
        var gameStateNode = GetNode<GameState>("/root/GameState");
        gameStateNode.CurrentTilemapData = tilemapData;
        gameStateNode.PlayerPosition = playerPosition;
        gameStateNode.PlayerId = playerId;
        
        // Store the complete player info
        gameStateNode.OtherPlayerInfo = playerInfo;
        
        // Extract positions for backward compatibility
        var playerPositions = new Godot.Collections.Dictionary<string, Vector2I>();
        foreach (var kvp in playerInfo)
        {
            playerPositions[kvp.Key] = (Vector2I)kvp.Value["Position"];
            
            // If the player is in a fight, add them to the PlayersInFight dictionary
            if (!string.IsNullOrEmpty(kvp.Value["FightId"].AsString()))
            {
                gameStateNode.PlayersInFight[kvp.Key] = true;
            }
        }
        gameStateNode.OtherPlayers = playerPositions;
        
        GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
    }

    private void OnJoinMapFailed(string error)
    {
        _statusLabel.Text = $"Failed to join map: {error}";
        _refreshButton.Disabled = false;
    }

    private void OnConnectionLost()
    {
        GetTree().ChangeSceneToFile("res://scenes/Title.tscn");
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _network.MapListReceived -= OnMapListReceived;
        _network.JoinMapInitiated -= OnJoinMapInitiated;
        _network.JoinMapCompleted -= OnJoinMapCompleted;
        _network.JoinMapFailed -= OnJoinMapFailed;
        _network.ConnectionLost -= OnConnectionLost;
    }
}
