using System.Collections.Generic;
using System.Text.Json;

public enum MovementDirection
{
    UP,
    DOWN,
    LEFT,
    RIGHT,
    ERROR,
};

public enum TankAction
{
    MOVE,
    SHOOT,
    ROTATE,
    ERROR,
}

public static class Utils
{
    public static List<PlayerAction> GetActionList(JsonElement actionElements)
    {
        var actionList = new System.Collections.Generic.List<PlayerAction>();

        foreach (JsonElement actionNode in actionElements.EnumerateArray())
        {
            if (!actionNode.TryGetProperty("type", out JsonElement typeEl))
                continue;

            string typeStr = typeEl.GetString()?.ToLower() ?? "";
            TankAction type = Utils.GetTankAction(typeStr);

            if (type == TankAction.ERROR)
                continue;

            PlayerAction action = new PlayerAction { Type = type };

            if (type == TankAction.MOVE)
            {
                if (!actionNode.TryGetProperty("direction", out JsonElement dirEl))
                    continue;
                action.Direction = Utils.GetMovementDirection(dirEl.GetString()?.ToLower() ?? "");
            }
            else if (type == TankAction.ROTATE)
            {
                if (!actionNode.TryGetProperty("degrees", out JsonElement degEl))
                    continue;
                float degrees = (float)degEl.GetDouble();
                if (!float.IsFinite(degrees))
                    continue;
                action.Degrees = degrees;
            }

            actionList.Add(action);
        }
        return actionList;
    }

    public static MovementDirection GetMovementDirection(string dir)
    {
        switch (dir)
        {
            case "up":    return MovementDirection.UP;
            case "down":  return MovementDirection.DOWN;
            case "left":  return MovementDirection.LEFT;
            case "right": return MovementDirection.RIGHT;
            default:      return MovementDirection.ERROR;
        }
    }

    public static TankAction GetTankAction(string action)
    {
        switch (action)
        {
            case "shoot":  return TankAction.SHOOT;
            case "move":   return TankAction.MOVE;
            case "rotate": return TankAction.ROTATE;
            default:       return TankAction.ERROR;
        }
    }
}
