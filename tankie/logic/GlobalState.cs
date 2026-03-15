using System.Collections.Generic;
using Godot;

public partial class GlobalState : Node
{
	public static GlobalState Instance { get; private set; }
	public static Dictionary<string, string> ConnectedPlayers = new Dictionary<string, string>();

	public static HashSet<(Vector2I, int)> WallRegistry = new HashSet<(Vector2I, int)>();

	[Export]
	public int GridSize = 128;

	public override void _Ready()
	{
		Instance = this;
	}

	public static void RegisterWall(Vector2I pos, int orientation)
	{
		WallRegistry.Add((pos, orientation));
	}
}
