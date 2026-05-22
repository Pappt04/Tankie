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
                    if (!root.TryGetProperty("tankId", out JsonElement idEl))
                        continue;
                    string tankId = idEl.GetString() ?? "";

                    if (string.IsNullOrWhiteSpace(tankId))
                    {
                        GD.Print("Rejected join: empty tankId");
                        continue;
                    }

                    // Reject if this name is already taken by a different WebSocket
                    bool nameTaken = false;
                    foreach (var kv in GameServer.ClientTankIds)
                        if (kv.Value == tankId && kv.Key != cmdData.Client)
                        { nameTaken = true; break; }
                    if (nameTaken)
                    {
                        GD.Print($"Rejected join: '{tankId}' already registered to another connection");
                        continue;
                    }

                    // Reject if this WebSocket already registered under a different name
                    if (GameServer.ClientTankIds.TryGetValue(cmdData.Client, out string existing) && existing != tankId)
                    {
                        GD.Print("Rejected join: connection already registered under a different name");
                        continue;
                    }

                    if (!GlobalState.ConnectedPlayers.ContainsKey(tankId))
                    {
                        GameServer.RegisterTankId(cmdData.Client, tankId);
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
