using System.Collections.Generic;

public struct PlayerAction
{
    public TankAction Type;
    public MovementDirection Direction; // for MOVE
    public float Degrees; // for ROTATE
}

public struct PlayerActions
{
    public string TankId;
    public List<PlayerAction> Actions;
}
