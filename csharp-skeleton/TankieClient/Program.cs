/**
 * Tankie – C# WebSocket client skeleton.
 *
 * Usage:
 *   dotnet run
 *
 * Make sure the Godot game is running and showing the Menu screen before
 * you connect.  Press "Start" in-game once all players have joined.
 */

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TankieClient;

internal class Program
{
    // ------------------------------------------------------------------ //
    // Configuration – change TankId to your chosen player name
    // ------------------------------------------------------------------ //
    const string ServerUri = "ws://localhost:8080/";
    const string RestBase  = "http://localhost:8080";
    const string TankId    = "player1";

    static readonly HttpClient Http = new();

    // ------------------------------------------------------------------ //
    // Entry point
    // ------------------------------------------------------------------ //
    static async Task Main(string[] args)
    {
        while (true)
        {
            try
            {
                var state = new GameState(TankId);
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(ServerUri), CancellationToken.None);
                Console.WriteLine($"Connected to {ServerUri} as '{TankId}'");

                await SendAsync(ws, MakeJoin(TankId));

                var buffer = new byte[8192];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var data = JsonNode.Parse(json)!.AsObject();

                    Dispatch(data, state);

                    // On game start fetch full positions (posX/posY) for all tanks
                    if (data["event"]?.GetValue<string>() == "game_started")
                    {
                        var playersArr = await FetchPlayersAsync();
                        if (playersArr != null) state.HandlePlayersSnapshot(playersArr);
                    }

                    // Act when it is our turn
                    if (state.IsMyTurn)
                    {
                        var actions = Strategy.DecideActions(state);
                        Console.WriteLine($"[action] sending {actions.Count} action(s)");
                        await SendAsync(ws, MakeCommand(TankId, actions));
                    }
                }

                Console.WriteLine("Disconnected. Reconnecting in 2s...");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[error] {e.Message} — retrying in 2s...");
            }

            await Task.Delay(2000);
        }
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
    // REST helpers – async HTTP GET (call sparingly, not every turn)
    // ------------------------------------------------------------------ //

    /// <summary>Wall list and gridSize.</summary>
    public static Task<JsonObject?> FetchMapAsync() => RestGetAsync("/map");

    /// <summary>All living players' positions and turret angles.</summary>
    public static async Task<JsonArray?> FetchPlayersAsync()
    {
        var obj = await RestGetAsync("/players");
        return obj?["players"]?.AsArray();
    }

    /// <summary>Single player state, or null if not found.</summary>
    public static async Task<JsonObject?> FetchPlayerAsync(string tankId)
    {
        try   { return await RestGetAsync($"/player/{tankId}"); }
        catch { return null; }
    }

    /// <summary>Game status: gameStarted, gameOver, onTurn, round, scores.</summary>
    public static Task<JsonObject?> FetchStateAsync() => RestGetAsync("/state");

    /// <summary>Physics constants for bullet trajectory math.</summary>
    public static Task<JsonObject?> FetchConstantsAsync() => RestGetAsync("/constants");

    /// <summary>Seconds remaining in the current turn.
    /// Returns: {turnTimeRemainingSeconds, onTurn}</summary>
    public static Task<JsonObject?> FetchTurnTimeAsync() => RestGetAsync("/turn_time");

    static async Task<JsonObject?> RestGetAsync(string path)
    {
        var json = await Http.GetStringAsync(RestBase + path);
        return JsonNode.Parse(json)?.AsObject();
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

            case "turn_timeout":
                var timedOutId = data["tankId"]!.GetValue<string>();
                Console.WriteLine($"[timeout] {timedOutId} timed out — turn skipped (1st warning)");
                break;

            case "turn_disqualified":
                var dqId = data["tankId"]!.GetValue<string>();
                Console.WriteLine($"[disqualified] {dqId} timed out twice — eliminated");
                break;

            case "game_over":
                var winner = data["winner"]?.GetValue<string>() ?? "";
                state.HandleGameOver(winner);
                var round = data["round"]?.GetValue<int>() ?? 0;
                var scoresNode = data["scores"]?.AsObject();
                var scoreStr = scoresNode != null
                    ? string.Join("  ", scoresNode.Select(kv => $"{kv.Key}: {kv.Value}"))
                    : "";
                var result = string.IsNullOrEmpty(winner) ? "[game] Draw!" : $"[game] {winner} wins!";
                Console.WriteLine($"{result} Round {round} | Scores: {scoreStr} | Waiting for next round...");
                break;

            case "lobby_reset":
                Console.WriteLine("[lobby] Server reset lobby — reconnecting...");
                break;

            default:
                Console.WriteLine($"[unknown event] {data.ToJsonString()}");
                break;
        }
    }
}
