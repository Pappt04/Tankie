using System;
using System.Collections.Generic;

namespace TankieClient;

public enum ACTION_DIRECTIONS { UP = 0, DOWN = 1, LEFT = 2, RIGHT = 3 };

/// <summary>
/// Strategy module – implement <see cref="DecideActions"/> to control your tank.
///
/// Action budget per turn:
///   • Up to 2 MOVE actions, OR
///   • 1 MOVE + 1 SHOOT, OR
///   • 1 SHOOT only
///   • Up to 1 ROTATE action (free – no budget cost)
///
/// Action object shapes (serialised to JSON by the client):
///   new MoveAction("up"|"down"|"left"|"right")
///   new RotateAction(degrees)          // 0–359
///   new ShootAction()
/// </summary>
public static class Strategy
{
    private static readonly Random Rng = new();
    private static readonly string[] Directions = ["up", "down", "left", "right"];

    /// <summary>
    /// Return the list of actions to execute this turn.
    /// The list is sent as-is; respect the budget or the server will reject it.
    /// </summary>
    public static List<IAction> DecideActions(GameState state)
    {
        // TODO: replace the random strategy below with your own logic.
        var actions = new List<IAction>();

        // Rotate to a random angle (free action – no budget cost).
        actions.Add(new RotateAction(Rng.Next(0, 360)));

        // Move in a random direction (costs 1 budget unit).
        actions.Add(new MoveAction(Directions[Rng.Next(Directions.Length)]));

        // Shoot (costs 1 budget unit – together with the move above this
        // exhausts the 2-unit budget, so no further moves are allowed).
        actions.Add(new ShootAction());

        return actions;
    }
}

// ------------------------------------------------------------------ //
// Action types
// ------------------------------------------------------------------ //

public interface IAction
{
    /// <summary>Serialise to the JSON object the server expects.</summary>
    object ToJson();
}

public record MoveAction(string Direction) : IAction
{
    public object ToJson() => new { type = "move", direction = Direction };
}

public record RotateAction(int Degrees) : IAction
{
    public object ToJson() => new { type = "rotate", degrees = Degrees };
}

public record ShootAction : IAction
{
    public object ToJson() => new { type = "shoot" };
}
