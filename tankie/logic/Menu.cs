using Godot;
using System;
using System.Text.Json;

public partial class Menu : Control
{
    private Label _playersLabel;

    public override void _Ready()
    {
        _playersLabel = GetNode<Label>("VBoxContainer/PlayersLabel");
        GetNode<Button>("VBoxContainer/StartButton").Pressed += OnStartPressed;
    }

    public override void _Process(double delta)
    {
        while (GameServer.CommandQueue.TryDequeue(out GameServer.CommandData cmdData))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(cmdData.Message);
                JsonElement root = doc.RootElement;
                string action = root.GetProperty("action").GetString();

                if (action == "join")
                {
                    string tankId = root.GetProperty("tankId").GetString();
                    if (!GlobalState.ConnectedPlayers.ContainsKey(tankId))
                    {
                        GlobalState.ConnectedPlayers.Add(tankId, tankId);
                        UpdatePlayersLabel();
                        GD.Print($"{tankId} joined the lobby!");
                        GameServer.Instance?.BroadcastMessage($"{{\"event\": \"player_joined\", \"tankId\": \"{tankId}\"}}");
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr("Failed to parse menu command: " + e.Message);
            }
        }
    }

    private void UpdatePlayersLabel()
    {
        _playersLabel.Text = "Connected Players:\n" + string.Join("\n", GlobalState.ConnectedPlayers.Keys);
    }

    private void OnStartPressed()
    {
        GD.Print($"Start pressed. Connected players: {GlobalState.ConnectedPlayers.Count}");
        if (GlobalState.ConnectedPlayers.Count > 0) 
        {
            Error err = GetTree().ChangeSceneToFile("res://scenes/main.tscn");
            if (err != Error.Ok)
            {
                GD.PrintErr($"Failed to change scene to main.tscn: {err}");
            }
        }
        else
        {
            GD.Print("Waiting for at least 1 player to join...");
            _playersLabel.Text = "Connected Players:\n(Wait for at least 1 player!)";
        }
    }
}
