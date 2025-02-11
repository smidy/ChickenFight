using Godot;
using System;
using System.Threading.Tasks;

public partial class Title : Node2D
{
    private Label _statusLabel = null!;
    private Button _connectButton = null!;
    private NetworkManager _network = null!;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("UI/StatusLabel");
        _connectButton = GetNode<Button>("UI/ConnectButton");
        _network = GetNode<NetworkManager>("/root/NetworkManager");

        _network.ConnectionConfirmed += OnConnectionConfirmed;
        _connectButton.Pressed += OnConnectPressed;
    }

    private async void OnConnectPressed()
    {
        _statusLabel.Text = "Connecting...";
        _connectButton.Disabled = true;

        await _network.Connect();

        if (!_network.IsConnected)
        {
            _statusLabel.Text = "Connection failed!";
            _connectButton.Disabled = false;
        }
    }

    private void OnConnectionConfirmed(string sessionId)
    {
        _statusLabel.Text = "Connected!";
        GetTree().CreateTimer(1.0).Timeout += () =>
        {
            GetTree().ChangeSceneToFile("res://scenes/MapSelect.tscn");
        };
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        _network.ConnectionConfirmed -= OnConnectionConfirmed;
    }
}
