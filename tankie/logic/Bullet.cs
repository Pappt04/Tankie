using Godot;

public partial class Bullet : CharacterBody2D
{
	[Export]
	public float Speed = 600.0f;
	public string OwnerId = "";

	private int _bouncesLeft = 2;

	public override void _Ready()
	{
		Velocity = Transform.X * Speed;

		float gs = GlobalState.Instance?.GridSize ?? 128f;
		float targetRadius = gs * 0.08f;

		Sprite2D sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite != null)
		{
			float texW = sprite.Texture.GetSize().X;
			float s = (targetRadius * 2) / texW;
			sprite.Scale = new Vector2(s, s);
		}

		CollisionShape2D collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collision != null && collision.Shape is CircleShape2D circle)
		{
			circle.Radius = targetRadius;
		}
	}

	private void SpawnExplosion(Vector2 pos)
	{
		float gs = GlobalState.Instance?.GridSize ?? 128f;
		float size = gs * 0.75f;

		string[] frames =
		{
			"res://resources/explosion2.png",
			"res://resources/explosion3.png",
			"res://resources/explosion4.png",
			"res://resources/explosion5.png",
		};

		Sprite2D exp = new Sprite2D();
		exp.ZIndex = 20;
		GetParent().AddChild(exp);
		exp.GlobalPosition = pos;

		void ShowFrame(int index)
		{
			if (!IsInstanceValid(exp))
				return;
			if (index >= frames.Length)
			{
				exp.QueueFree();
				return;
			}
			var tex = GD.Load<Texture2D>(frames[index]);
			exp.Texture = tex;
			exp.Scale = Vector2.One * (size / tex.GetSize().X);
			exp.GetTree().CreateTimer(0.1f).Timeout += () => ShowFrame(index + 1);
		}

		ShowFrame(0);
	}

	public override void _PhysicsProcess(double delta)
	{
		var collision = MoveAndCollide(Velocity * (float)delta);

		if (collision != null)
		{
			Node collider = (Node)collision.GetCollider();

			// 1. Check if we hit a player
			if (collider is Player player)
			{
				GD.Print($"Bullet from {OwnerId} hit player: {player.Name}");
				SpawnExplosion(player.GlobalPosition);
				player.QueueFree();
				QueueFree();
				return;
			}

			// 2. Handle Bouncing
			if (_bouncesLeft > 0)
			{
				_bouncesLeft--;
				Velocity = Velocity.Bounce(collision.GetNormal());
				Rotation = Velocity.Angle();
				GD.Print($"Bullet bounced! Bounces left: {_bouncesLeft}");
			}
			else
			{
				GD.Print("Bullet max bounces reached. Destroying.");
				QueueFree();
			}
		}
	}
}
