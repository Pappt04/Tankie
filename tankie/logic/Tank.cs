using Godot;

public partial class Tank : CharacterBody2D
{
	public int PlayerIndex = 0; // 0 = blue, 1 = red

	private Vector2I _gridPos;

	public Vector2I GridPos
	{
		get => _gridPos;
		set
		{
			_gridPos = value;
			float gs = GlobalState.Instance?.GridSize ?? 128f;
			Vector2 target = new Vector2(_gridPos.X * gs + gs / 2, _gridPos.Y * gs + gs / 2);
			ZIndex = 10;

			if (!IsInsideTree())
			{
				Position = target;
				return;
			}

			MoveTween?.Kill();
			MoveTween = CreateTween();
			MoveTween
				.TweenProperty(this, "position", target, 0.5f)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.InOut);
		}
	}

	protected Node2D _turret;
	protected Marker2D _muzzle;
	private PackedScene _bulletScene = GD.Load<PackedScene>("res://scenes/bullet.tscn");

	public Tween MoveTween   { get; private set; }
	public Tween RotateTween { get; private set; }

	private static readonly string[] BodyTextures =
	{
		"res://resources/blue_tank_body.png",
		"res://resources/red_tank_body.png",
	};
	private static readonly string[] TurretTextures =
	{
		"res://resources/blue_tank_turret.png",
		"res://resources/red_tank_turret.png",
	};

	public override void _Ready()
	{
		_turret = GetNode<Node2D>("Turret");
		_muzzle = _turret.GetNode<Marker2D>("Muzzle");

		float gs = GlobalState.Instance?.GridSize ?? 128f;

		// Body
		Sprite2D bodySprite = GetNode<Sprite2D>("Body");
		if (bodySprite != null)
		{
			bodySprite.Texture = GD.Load<Texture2D>(BodyTextures[PlayerIndex]);
			float texSize = Mathf.Max(
				bodySprite.Texture.GetSize().X,
				bodySprite.Texture.GetSize().Y
			);
			float s = (gs * 0.65f) / texSize;
			bodySprite.Scale = new Vector2(s, s);
		}

		// Collision
		CollisionShape2D collision = GetNode<CollisionShape2D>("CollisionShape2D");
		if (collision != null && collision.Shape is RectangleShape2D rect)
			rect.Size = new Vector2(gs * 0.5f, gs * 0.5f);

		// Turret barrel
		Sprite2D turretSprite = _turret.GetNode<Sprite2D>("TurretSprite");
		if (turretSprite != null)
		{
			turretSprite.Texture = GD.Load<Texture2D>(TurretTextures[PlayerIndex]);
			float targetLength = gs * 0.42f;
			float s = targetLength / turretSprite.Texture.GetSize().X;
			turretSprite.Scale = new Vector2(s, s);
			turretSprite.Position = new Vector2(targetLength * 0.5f, 0);
		}

		// Muzzle tip
		if (_muzzle != null)
			_muzzle.Position = new Vector2(gs * 0.42f, 0);

		// Turret fire flash
		Sprite2D fire = _turret.GetNodeOrNull<Sprite2D>("TurretFire");
		if (fire != null)
		{
			fire.Texture = GD.Load<Texture2D>("res://resources/turret_fire.png");
			float targetWidth = gs * 0.32f;
			float s = targetWidth / fire.Texture.GetSize().X;
			fire.Scale = new Vector2(s, s);
			// Left edge at muzzle tip: center = muzzle_x + half_scaled_width
			fire.Position = new Vector2(gs * 0.42f + targetWidth * 0.5f, 0);
			fire.Visible = false;
		}
	}

	public void MoveTurnBased(MovementDirection direction)
	{
		if (direction == MovementDirection.ERROR)
		{
			GD.Print("No valid movement direction");
			return;
		}

		Vector2I moveVector = Vector2I.Zero;
		bool isBlocked = false;

		switch (direction)
		{
			case MovementDirection.UP:
				moveVector = Vector2I.Up;
				isBlocked = GlobalState.WallRegistry.Contains(
					(GridPos + Vector2I.Up, (int)WallOrientation.HORIZONTAL)
				);
				break;
			case MovementDirection.DOWN:
				moveVector = Vector2I.Down;
				isBlocked = GlobalState.WallRegistry.Contains(
					(GridPos, (int)WallOrientation.HORIZONTAL)
				);
				break;
			case MovementDirection.LEFT:
				moveVector = Vector2I.Left;
				isBlocked = GlobalState.WallRegistry.Contains(
					(GridPos + Vector2I.Left, (int)WallOrientation.VERTICAL)
				);
				break;
			case MovementDirection.RIGHT:
				moveVector = Vector2I.Right;
				isBlocked = GlobalState.WallRegistry.Contains(
					(GridPos, (int)WallOrientation.VERTICAL)
				);
				break;
		}

		if (isBlocked)
		{
			GD.Print($"Movement {direction} blocked by wall at grid {GridPos}");
			return;
		}

		SpawnTrackMark(direction);
		GridPos += moveVector;
	}

	private void SpawnTrackMark(MovementDirection direction)
	{
		var tex = GD.Load<Texture2D>("res://resources/track_marks.png");
		if (tex == null)
			return;

		float gs = GlobalState.Instance?.GridSize ?? 128f;
		Vector2 worldPos = GlobalPosition;

		Sprite2D mark = new Sprite2D();
		mark.Texture = tex;
		mark.ZIndex = 0;
		mark.Scale = new Vector2(gs / tex.GetSize().X, gs / tex.GetSize().Y);

		if (direction == MovementDirection.UP || direction == MovementDirection.DOWN)
			mark.RotationDegrees = 90;

		GetParent()?.AddChild(mark);
		mark.GlobalPosition = worldPos;
	}

	public void RotateTurret(float degrees)
	{
		if (_turret == null)
			return;

		// Shortest angular path to avoid spinning the long way around
		float diff = Mathf.Wrap(degrees - _turret.RotationDegrees, -180f, 180f);

		RotateTween?.Kill();
		RotateTween = CreateTween();
		RotateTween
			.TweenProperty(_turret, "rotation_degrees", _turret.RotationDegrees + diff, 0.35f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
	}

	public virtual Bullet Shoot()
	{
		if (_bulletScene == null)
			return null;

		Bullet bullet = _bulletScene.Instantiate<Bullet>();
		bullet.GlobalPosition = _muzzle.GlobalPosition;
		bullet.GlobalRotation = _turret.GlobalRotation;
		bullet.OwnerId = Name;
		GetTree().Root.AddChild(bullet);

		// Flash turret fire for 150 ms
		Sprite2D fire = _turret.GetNodeOrNull<Sprite2D>("TurretFire");
		if (fire != null)
		{
			fire.Visible = true;
			GetTree().CreateTimer(0.3f).Timeout += () =>
			{
				if (IsInstanceValid(fire))
					fire.Visible = false;
			};
		}

		return bullet;
	}
}
