using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace TankieClient;

/// <summary>
/// Snapshot of a single tank as reported by the server.
/// </summary>
public class TankState
{
    public string TankId        { get; init; } = "";
    public float  PosX          { get; set; }
    public float  PosY          { get; set; }
    public int    GridX         { get; set; }
    public int    GridY         { get; set; }
    public float  TurretDegrees { get; set; }
    public bool   Alive         { get; set; } = true;
}

/// <summary>
/// Local mirror of the game state, updated from incoming server events.
/// </summary>
public class GameState
{
    public string MyTankId    { get; }
    public string OnTurn      { get; private set; } = "";
    public bool   GameStarted { get; private set; }
    public bool   GameOver    { get; private set; }
    public string? Winner     { get; private set; }

    // Map data – populated by the "map" event at game start
    public int GridWidth  { get; private set; }
    public int GridHeight { get; private set; }
    public int GridSize   { get; private set; } = 128;

    private readonly Dictionary<string, TankState> _tanks = new();
    private readonly HashSet<(int X, int Y, string Orientation)> _walls = new();

    public GameState(string myTankId)
    {
        MyTankId = myTankId;
    }

    // ------------------------------------------------------------------ //
    // Event handlers – call these from the main loop when an event arrives
    // ------------------------------------------------------------------ //

    public void HandlePlayerJoined(string tankId)
    {
        if (!_tanks.ContainsKey(tankId))
            _tanks[tankId] = new TankState { TankId = tankId };
    }

    public void HandleMap(JsonObject data)
    {
        GameOver    = false;
        GameStarted = false;
        _tanks.Clear();

        GridWidth  = data["gridWidth"]!.GetValue<int>();
        GridHeight = data["gridHeight"]!.GetValue<int>();
        GridSize   = data["gridSize"]!.GetValue<int>();

        _walls.Clear();
        foreach (var wallNode in data["walls"]!.AsArray())
        {
            var w = wallNode!.AsObject();
            _walls.Add((
                w["x"]!.GetValue<int>(),
                w["y"]!.GetValue<int>(),
                w["orientation"]!.GetValue<string>()
            ));
        }

        foreach (var playerNode in data["players"]!.AsArray())
        {
            var p      = playerNode!.AsObject();
            var tankId = p["tankId"]!.GetValue<string>();
            if (!_tanks.ContainsKey(tankId))
                _tanks[tankId] = new TankState { TankId = tankId };
            _tanks[tankId].GridX = p["x"]!.GetValue<int>();
            _tanks[tankId].GridY = p["y"]!.GetValue<int>();
        }
    }

    public void HandleGameStarted(string onTurn)
    {
        GameStarted = true;
        OnTurn = onTurn;
    }

    public void HandleTurnChanged(string nextTurn)
    {
        OnTurn = nextTurn;
    }

    public void HandleMoved(string tankId, string direction, float posX, float posY)
    {
        if (_tanks.TryGetValue(tankId, out var tank))
        {
            tank.PosX  = posX;
            tank.PosY  = posY;
            tank.GridX = (int)posX / GridSize;
            tank.GridY = (int)posY / GridSize;
        }
    }

    public void HandleRotated(string tankId, float degrees)
    {
        if (_tanks.TryGetValue(tankId, out var tank))
            tank.TurretDegrees = degrees;
    }

    public void HandleGameOver(string winner)
    {
        GameOver = true;
        Winner   = winner;
    }

    // ------------------------------------------------------------------ //
    // Convenience helpers
    // ------------------------------------------------------------------ //

    /// <summary>True when the game is running and it is our turn.</summary>
    public bool IsMyTurn => GameStarted && OnTurn == MyTankId;

    public TankState? MyTank() => _tanks.GetValueOrDefault(MyTankId);

    public IEnumerable<TankState> Opponents() =>
        _tanks.Values.Where(t => t.TankId != MyTankId && t.Alive);

    /// <summary>Check if a wall exists at (x, y) with the given orientation.</summary>
    public bool HasWall(int x, int y, string orientation) =>
        _walls.Contains((x, y, orientation));
}
