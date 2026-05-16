using Godot;

public enum WallOrientation
{
	HORIZONTAL,
	VERTICAL,
}

public partial class Wall : StaticBody2D
{
	private Vector2I _gridPos;
	private WallOrientation _orientation;

	public void Setup(Vector2I gridPos, WallOrientation orientation)
	{
		_gridPos = gridPos;
		_orientation = orientation;

		float gs = GlobalState.Instance?.GridSize ?? 128f;
		float thickness = gs * 0.078f;
		float length = gs * 1.1f;

		if (_orientation == WallOrientation.HORIZONTAL)
		{
			Position = new Vector2(gridPos.X * gs + gs / 2, (gridPos.Y + 1) * gs);
			RotationDegrees = 0;
		}
		else
		{
			Position = new Vector2((gridPos.X + 1) * gs, gridPos.Y * gs + gs / 2);
			RotationDegrees = 90;
		}

		Sprite2D sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite != null)
		{
			float texHeight = sprite.Texture?.GetSize().Y ?? 128f;
			float contentX = 37f;
			float contentWidth = 59f;
			sprite.RegionEnabled = true;
			sprite.RegionRect = new Rect2(contentX, 0, contentWidth, texHeight);
			sprite.Scale = new Vector2(length / contentWidth, thickness / texHeight);
			sprite.SelfModulate = Colors.Black;
		}

		CollisionShape2D collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collision != null)
		{
			RectangleShape2D newShape = new RectangleShape2D();
			newShape.Size = new Vector2(length, thickness);
			collision.Shape = newShape;
		}

		ZIndex = 5;
	}

	public override void _Ready()
	{
		// Logic handles entirely inside Setup
	}
}
