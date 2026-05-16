/**
 * Tankie – C# WebSocket client skeleton.
 *
 * Usage:
 *   dotnet run
 *
 * Make sure the Godot game is running and showing the Menu screen before
 * you connect.  Press "Start" in-game once all players have joined.
 */

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TankieClient;

// ------------------------------------------------------------------ //
// Configuration – change TankId to your chosen player name
// ------------------------------------------------------------------ //
const string ServerUri = "ws://localhost:8080/";
const string TankId    = "player1";

// ------------------------------------------------------------------ //
// Run
// ------------------------------------------------------------------ //
await RunAsync();

async Task RunAsync()
{
    var state = new GameState(TankId);

    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri(ServerUri), CancellationToken.None);
    Console.WriteLine($"Connected to {ServerUri} as '{TankId}'");

    // Join the game
    await SendAsync(ws, MakeJoin(TankId));

    // Main receive loop
    var buffer = new byte[8192];
    while (ws.State == WebSocketState.Open)
    {
        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Close)
            break;

        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var data = JsonNode.Parse(json)!.AsObject();

        Dispatch(data, state);

        // Act when it is our turn
        if (state.IsMyTurn)
        {
            var actions = Strategy.DecideActions(state);
            Console.WriteLine($"[action] sending {actions.Count} action(s)");
            await SendAsync(ws, MakeCommand(TankId, actions));
        }
    }

    Console.WriteLine("Disconnected.");
}

// ------------------------------------------------------------------ //
// Message helpers
// ------------------------------------------------------------------ //

static string MakeJoin(string tankId) =>
    JsonSerializer.Serialize(new { action = "join", tankId });

static string MakeCommand(string tankId, List<IAction> actions)
{
    var actionJsons = actions.ConvertAll(a => a.ToJson());
    return JsonSerializer.Serialize(new { tankId, actions = actionJsons });
}

static async Task SendAsync(ClientWebSocket ws, string message)
{
    var bytes = Encoding.UTF8.GetBytes(message);
    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
}

// ------------------------------------------------------------------ //
// Event dispatcher
// ------------------------------------------------------------------ //

static void Dispatch(JsonObject data, GameState state)
{
    var evt = data["event"]?.GetValue<string>();

    switch (evt)
    {
        case "player_joined":
            var joinedId = data["tankId"]!.GetValue<string>();
            state.HandlePlayerJoined(joinedId);
            Console.WriteLine($"[lobby] {joinedId} joined");
            break;

        case "map":
            state.HandleMap(data);
            Console.WriteLine($"[map] received {data["walls"]!.AsArray().Count} walls, " +
                              $"grid {data["gridWidth"]}x{data["gridHeight"]}");
            break;

        case "game_started":
            var onTurn = data["onTurn"]!.GetValue<string>();
            state.HandleGameStarted(onTurn);
            Console.WriteLine($"[game] started – first turn: {onTurn}");
            break;

        case "turn_changed":
            var nextTurn = data["nextTurn"]!.GetValue<string>();
            state.HandleTurnChanged(nextTurn);
            Console.WriteLine($"[game] turn → {nextTurn}");
            break;

        case "moved":
            state.HandleMoved(
                data["tankId"]!.GetValue<string>(),
                data["direction"]!.GetValue<string>(),
                data["pos_x"]!.GetValue<float>(),
                data["pos_y"]!.GetValue<float>());
            Console.WriteLine($"[move] {data["tankId"]} → {data["direction"]} " +
                              $"({data["pos_x"]}, {data["pos_y"]})");
            break;

        case "rotated":
            state.HandleRotated(
                data["tankId"]!.GetValue<string>(),
                data["degrees"]!.GetValue<float>());
            Console.WriteLine($"[rotate] {data["tankId"]} turret → {data["degrees"]}°");
            break;

        case "shot":
            Console.WriteLine($"[shot] {data["tankId"]} fired");
            break;

        case "game_over":
            var winner = data["winner"]?.GetValue<string>() ?? "";
            state.HandleGameOver(winner);
            Console.WriteLine(string.IsNullOrEmpty(winner)
                ? "[game] Draw! Waiting for next round..."
                : $"[game] {winner} wins! Waiting for next round...");
            break;

        default:
            Console.WriteLine($"[unknown event] {data.ToJsonString()}");
            break;
    }
}
