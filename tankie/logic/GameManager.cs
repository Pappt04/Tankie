using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using Godot;

public partial class GameManager : Node2D
{
	public static GameManager Instance { get; private set; }

	// ── Thread-safe snapshot read by the REST endpoints ──────────────
	public class PlayerSnapshot
	{
		public string TankId       { get; set; }
		public int    GridX        { get; set; }
		public int    GridY        { get; set; }
		public float  PosX         { get; set; }
		public float  PosY         { get; set; }
		public float  TurretDegrees { get; set; }
	}

	public class GameSnapshot
	{
		public bool                        GameStarted { get; set; }
		public bool                        GameOver    { get; set; }
		public string                      OnTurn      { get; set; }
		public int                         Round       { get; set; }
		public Dictionary<string, int>     Scores      { get; set; }
		public List<PlayerSnapshot>        Players     { get; set; }
	}

	private GameSnapshot _snapshot = new GameSnapshot
		{ Scores = new Dictionary<string, int>(), Players = new List<PlayerSnapshot>() };
	private readonly object _snapshotLock = new object();

	public GameSnapshot GetSnapshot()
	{
		lock (_snapshotLock) return _snapshot;
	}

	private void UpdateSnapshot()
	{
		var snap = new GameSnapshot
		{
			GameStarted = _gameStarted,
			GameOver    = _gameOver,
			OnTurn      = (_gameStarted && players.Count > 0 && turnIndex < players.Count)
			              ? players[turnIndex].Name : "",
			Round       = GlobalState.RoundNumber,
			Scores      = new Dictionary<string, int>(GlobalState.Scores),
			Players     = new List<PlayerSnapshot>()
		};
		foreach (var p in players)
		{
			if (IsInstanceValid(p))
				snap.Players.Add(new PlayerSnapshot
				{
					TankId        = p.Name,
					GridX         = p.GridPos.X,
					GridY         = p.GridPos.Y,
					PosX          = p.Position.X,
					PosY          = p.Position.Y,
					TurretDegrees = p.TurretDegrees
				});
		}
		lock (_snapshotLock) _snapshot = snap;
	}
	// ─────────────────────────────────────────────────────────────────

	private System.Collections.Generic.List<Player> players =
		new System.Collections.Generic.List<Player>();
	private PackedScene _playerScene = GD.Load<PackedScene>("res://scenes/player.tscn");

	private int turnIndex = 0;
	private PackedScene _wallScene = GD.Load<PackedScene>("res://scenes/wall.tscn");

	private bool _gameStarted = false;
	private bool _gameOver = false;
	private bool _animating = false;
	private int _gridW;
	private int _gridH;
	private Label _overlayLabel;
	private Label _roundLabel;
	private Label[] _scoreLabels;
	private CanvasLayer _canvas;

	public override void _Ready()
	{
		Instance = this;

		TextureRect bgTiles = GetNodeOrNull<TextureRect>("BackgroundTiles");
		if (bgTiles != null)
		{
			float gs = GlobalState.Instance?.GridSize ?? 128f;
			float texSize = bgTiles.Texture.GetSize().X;
			float scale = gs / texSize;
			bgTiles.Scale = new Vector2(scale, scale);
		}

		GenerateMaze();

		Vector2 viewportSize = GetViewportRect().Size;
		int gs2 = GlobalState.Instance?.GridSize ?? 128;
		_gridW = (int)(viewportSize.X / gs2);
		_gridH = (int)(viewportSize.Y / gs2);

		Vector2I[] spawnPoints =
		{
			new Vector2I(1, 1),
			new Vector2I(_gridW - 2, _gridH - 2),
		};

		int i = 0;
		foreach (var p in GlobalState.ConnectedPlayers)
		{
			Player newTank = _playerScene.Instantiate<Player>();
			newTank.Name = p.Key;
			newTank.PlayerIndex = i; // 0 = blue, 1 = red
			AddChild(newTank);
			newTank.GridPos = spawnPoints[i];
			players.Add(newTank);
			++i;
		}

		SetupOverlayLabel();
		UpdateSnapshot();
		StartCountdown();
	}

	private void SetupOverlayLabel()
	{
		_canvas = new CanvasLayer();
		_canvas.Layer = 10;
		AddChild(_canvas);

		// Big centred countdown / winner label
		_overlayLabel = new Label();
		_overlayLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_overlayLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_overlayLabel.VerticalAlignment = VerticalAlignment.Center;
		_overlayLabel.AddThemeFontSizeOverride("font_size", 180);
		_overlayLabel.AddThemeColorOverride("font_color", new Color(1, 0.08f, 0.08f));
		_overlayLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
		_overlayLabel.AddThemeConstantOverride("outline_size", 10);
		_overlayLabel.Text = "";
		_canvas.AddChild(_overlayLabel);

		// Round counter — top-left
		_roundLabel = new Label();
		_roundLabel.AnchorLeft = 0;
		_roundLabel.AnchorRight = 0;
		_roundLabel.AnchorTop = 0;
		_roundLabel.AnchorBottom = 0;
		_roundLabel.OffsetLeft = 16;
		_roundLabel.OffsetRight = 220;
		_roundLabel.OffsetTop = 16;
		_roundLabel.OffsetBottom = 54;
		_roundLabel.AddThemeFontSizeOverride("font_size", 28);
		_roundLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
		_roundLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
		_roundLabel.AddThemeConstantOverride("outline_size", 5);
		_canvas.AddChild(_roundLabel);

		// Player name + score HUD — top-right corner
		Color[] teamColors = { new Color(0.25f, 0.6f, 1f), new Color(1f, 0.15f, 0.15f) };
		string[] teamNames = { "Blue", "Red" };

		_scoreLabels = new Label[players.Count];
		for (int i = 0; i < players.Count; i++)
		{
			Label hud = new Label();
			hud.AnchorLeft = 1;
			hud.AnchorRight = 1;
			hud.AnchorTop = 0;
			hud.AnchorBottom = 0;
			hud.GrowHorizontal = Control.GrowDirection.Begin;
			hud.OffsetRight = -16;
			hud.OffsetLeft = -340;
			hud.OffsetTop = 16 + i * 44;
			hud.OffsetBottom = hud.OffsetTop + 38;
			hud.HorizontalAlignment = HorizontalAlignment.Right;
			hud.AddThemeFontSizeOverride("font_size", 28);
			hud.AddThemeColorOverride("font_color", teamColors[i]);
			hud.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
			hud.AddThemeConstantOverride("outline_size", 5);
			_canvas.AddChild(hud);
			_scoreLabels[i] = hud;
		}

		UpdateHud();
	}

	private void UpdateHud()
	{
		_roundLabel.Text = $"Round {GlobalState.RoundNumber}";

		string[] teamNames = { "Blue", "Red" };
		for (int i = 0; i < players.Count && i < _scoreLabels.Length; i++)
		{
			string name = players[i].Name;
			int score = GlobalState.Scores.TryGetValue(name, out int s) ? s : 0;
			_scoreLabels[i].Text = $"● {name}  [{teamNames[i]}]  {score} pts";
		}
	}

	private void StartCountdown()
	{
		string[] steps = { "3", "2", "1", "GO!" };
		float[] delays = { 1.0f, 1.0f, 1.0f, 0.7f };

		void ShowStep(int idx)
		{
			if (idx >= steps.Length)
			{
				_overlayLabel.Text = "";
				_gameStarted = true;
				UpdateSnapshot();
				BroadcastMap();
				GameServer.Instance?.BroadcastMessage(
					$"{{\"event\": \"game_started\", \"onTurn\": \"{players[turnIndex].Name}\"}}"
				);
				return;
			}
			_overlayLabel.Text = steps[idx];
			GetTree().CreateTimer(delays[idx]).Timeout += () => ShowStep(idx + 1);
		}

		ShowStep(0);
	}

	private void BroadcastMap()
	{
		var sb = new System.Text.StringBuilder();
		sb.Append("{\"event\": \"map\"");
		sb.Append($", \"gridWidth\": {_gridW}");
		sb.Append($", \"gridHeight\": {_gridH}");
		sb.Append($", \"gridSize\": {GlobalState.Instance?.GridSize ?? 128}");

		sb.Append(", \"walls\": [");
		bool first = true;
		foreach (var (pos, orientation) in GlobalState.WallRegistry)
		{
			if (!first)
				sb.Append(", ");
			string orientStr = orientation == 0 ? "HORIZONTAL" : "VERTICAL";
			sb.Append($"{{\"x\": {pos.X}, \"y\": {pos.Y}, \"orientation\": \"{orientStr}\"}}");
			first = false;
		}
		sb.Append("]");

		sb.Append(", \"players\": [");
		first = true;
		foreach (var player in players)
		{
			if (!first)
				sb.Append(", ");
			sb.Append($"{{\"tankId\": \"{player.Name}\", \"x\": {player.GridPos.X}, \"y\": {player.GridPos.Y}}}");
			first = false;
		}
		sb.Append("]");

		sb.Append("}");
		GameServer.Instance?.BroadcastMessage(sb.ToString());
	}

	private void CheckWinner()
	{
		players.RemoveAll(p => !IsInstanceValid(p));

		if (players.Count == 1)
		{
			_gameStarted = false;
			_gameOver = true;
			GlobalState.RoundNumber++;
			string winner = players[0].Name;
			GlobalState.Scores.TryGetValue(winner, out int prev);
			GlobalState.Scores[winner] = prev + 1;
			UpdateHud();
			UpdateSnapshot();
			_overlayLabel.AddThemeFontSizeOverride("font_size", 100);
			_overlayLabel.Text = $"{winner}\nWins!\n\n[ENTER] Play again";
			GameServer.Instance?.BroadcastMessage(
				$"{{\"event\": \"game_over\", \"winner\": \"{winner}\", \"round\": {GlobalState.RoundNumber}, \"scores\": {BuildScoresJson()}}}"
			);
		}
		else if (players.Count == 0)
		{
			_gameStarted = false;
			_gameOver = true;
			GlobalState.RoundNumber++;
			UpdateHud();
			UpdateSnapshot();
			_overlayLabel.AddThemeFontSizeOverride("font_size", 100);
			_overlayLabel.Text = "Draw!\n\n[ENTER] Play again   [L] New lobby";
			GameServer.Instance?.BroadcastMessage(
				$"{{\"event\": \"game_over\", \"winner\": \"\", \"round\": {GlobalState.RoundNumber}, \"scores\": {BuildScoresJson()}}}"
			);
		}
	}

	private string BuildScoresJson()
	{
		var parts = new System.Collections.Generic.List<string>();
		foreach (var kv in GlobalState.Scores)
			parts.Add($"\"{kv.Key}\": {kv.Value}");
		return "{" + string.Join(", ", parts) + "}";
	}

	private void GenerateMaze()
	{
		Vector2 viewportSize = GetViewportRect().Size;
		int gs = GlobalState.Instance?.GridSize ?? 128;
		GD.Print($"Generating maze with GridSize: {gs}");

		int gridWidth = (int)(viewportSize.X / gs);
		int gridHeight = (int)(viewportSize.Y / gs);

		GlobalState.WallRegistry.Clear();

		// hWalls[x,y] = wall on the bottom edge of cell (x,y), separating (x,y) from (x,y+1)
		// vWalls[x,y] = wall on the right edge of cell (x,y), separating (x,y) from (x+1,y)
		bool[,] hWalls = new bool[gridWidth, gridHeight - 1];
		bool[,] vWalls = new bool[gridWidth - 1, gridHeight];

		// Start with all internal walls present
		for (int x = 0; x < gridWidth; x++)
		for (int y = 0; y < gridHeight - 1; y++)
			hWalls[x, y] = true;
		for (int x = 0; x < gridWidth - 1; x++)
		for (int y = 0; y < gridHeight; y++)
			vWalls[x, y] = true;

		// Recursive Backtracking DFS to carve a perfect maze (spanning tree)
		bool[,] visited = new bool[gridWidth, gridHeight];
		var rng = new Random();
		var stack = new Stack<Vector2I>();

		stack.Push(new Vector2I(0, 0));
		visited[0, 0] = true;

		// dx/dy and wall removal for: UP, DOWN, LEFT, RIGHT
		int[] dx = { 0, 0, -1, 1 };
		int[] dy = { -1, 1, 0, 0 };

		while (stack.Count > 0)
		{
			Vector2I cur = stack.Peek();
			int cx = cur.X,
				cy = cur.Y;

			// Collect unvisited neighbours
			var unvisited = new List<int>();
			for (int d = 0; d < 4; d++)
			{
				int nx = cx + dx[d],
					ny = cy + dy[d];
				if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight && !visited[nx, ny])
					unvisited.Add(d);
			}

			if (unvisited.Count == 0)
			{
				stack.Pop();
				continue;
			}

			int dir = unvisited[rng.Next(unvisited.Count)];
			int nnx = cx + dx[dir],
				nny = cy + dy[dir];

			// Carve passage: remove the wall between cur and neighbour
			switch (dir)
			{
				case 0:
					hWalls[cx, cy - 1] = false;
					break; // UP: remove bottom wall of cell above
				case 1:
					hWalls[cx, cy] = false;
					break; // DOWN: remove bottom wall of cur
				case 2:
					vWalls[cx - 1, cy] = false;
					break; // LEFT: remove right wall of cell to left
				case 3:
					vWalls[cx, cy] = false;
					break; // RIGHT: remove right wall of cur
			}

			visited[nnx, nny] = true;
			stack.Push(new Vector2I(nnx, nny));
		}

		// Remove ~15% of remaining walls to create loops (non-perfect maze)
		float loopChance = 0.15f;
		for (int x = 0; x < gridWidth; x++)
		for (int y = 0; y < gridHeight - 1; y++)
			if (hWalls[x, y] && rng.NextDouble() < loopChance)
				hWalls[x, y] = false;

		for (int x = 0; x < gridWidth - 1; x++)
		for (int y = 0; y < gridHeight; y++)
			if (vWalls[x, y] && rng.NextDouble() < loopChance)
				vWalls[x, y] = false;

		// Spawn boundary walls
		for (int x = 0; x < gridWidth; x++)
		{
			SpawnWall(new Vector2I(x, -1), WallOrientation.HORIZONTAL);
			SpawnWall(new Vector2I(x, gridHeight - 1), WallOrientation.HORIZONTAL);
		}
		for (int y = 0; y < gridHeight; y++)
		{
			SpawnWall(new Vector2I(-1, y), WallOrientation.VERTICAL);
			SpawnWall(new Vector2I(gridWidth - 1, y), WallOrientation.VERTICAL);
		}

		// Spawn internal walls
		for (int x = 0; x < gridWidth; x++)
		for (int y = 0; y < gridHeight - 1; y++)
			if (hWalls[x, y])
				SpawnWall(new Vector2I(x, y), WallOrientation.HORIZONTAL);

		for (int x = 0; x < gridWidth - 1; x++)
		for (int y = 0; y < gridHeight; y++)
			if (vWalls[x, y])
				SpawnWall(new Vector2I(x, y), WallOrientation.VERTICAL);

		GD.Print($"Maze generation complete. Walls spawned: {GetChildCount()}");
	}

	private void SpawnWall(Vector2I gridPos, WallOrientation orientation)
	{
		Wall wall = _wallScene.Instantiate<Wall>();
		wall.Setup(gridPos, orientation);
		AddChild(wall);
		GlobalState.RegisterWall(gridPos, (int)orientation);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			if (key.Keycode == Key.Escape)
				GetTree().Quit();

			if (key.Keycode == Key.Enter && _gameOver)
				GetTree().ReloadCurrentScene();

			if (key.Keycode == Key.L && _gameOver)
				ResetToLobby();
		}
	}

	private async void ResetToLobby()
	{
		GameServer.Instance?.BroadcastMessage("{\"event\": \"lobby_reset\"}");
		if (GameServer.Instance != null)
			await GameServer.Instance.DisconnectAllClients();
		GlobalState.ConnectedPlayers.Clear();
		GlobalState.Scores.Clear();
		GlobalState.RoundNumber = 0;
		GetTree().ChangeSceneToFile("res://scenes/menu.tscn");
	}

	public override void _Process(double delta)
	{
		if (_animating)
			return;

		if (GameServer.CommandQueue.TryDequeue(out GameServer.CommandData cmdData))
			ProcessCommandAsync(cmdData.Message, cmdData.Client);
	}

	private async void ProcessCommandAsync(string jsonMessage, WebSocket client)
	{
		if (!_gameStarted)
			return;

		try
		{
			CheckWinner();
			using JsonDocument doc = JsonDocument.Parse(jsonMessage);
			JsonElement root = doc.RootElement;

			string tankId = root.TryGetProperty("tankId", out JsonElement idElement)
				? idElement.GetString()
				: "";
			Player currentTank = players[turnIndex];

			if (tankId != currentTank.Name)
			{
				GD.Print($"It is {currentTank.Name}'s turn, not {tankId}");
				return;
			}

			if (
				!root.TryGetProperty("actions", out JsonElement actionsElement)
				|| actionsElement.ValueKind != JsonValueKind.Array
			)
			{
				GD.Print("No actions array found in message.");
				return;
			}

			var actionList = Utils.GetActionList(actionsElement);

			int moveCount = 0;
			int shootCount = 0;

			foreach (PlayerAction action in actionList)
				if (action.Type == TankAction.MOVE)
					moveCount++;
				else if (action.Type == TankAction.SHOOT)
					shootCount++;

			if (moveCount + shootCount > 2 || shootCount > 1)
			{
				GD.Print(
					$"Invalid action budget: Moves={moveCount}, Shoots={shootCount}. Allowed: 2 moves or 1 move + 1 shoot."
				);
				return;
			}

			_animating = true;
			await ExecuteActionsAsync(currentTank, actionList);
			_animating = false;
			UpdateSnapshot();

			CheckWinner();
			if (!_gameStarted)
				return;

			turnIndex = (turnIndex + 1) % players.Count;
			string nextTurnId = players[turnIndex].Name;
			GameServer.Instance?.BroadcastMessage(
				$"{{\"event\": \"turn_changed\", \"nextTurn\": \"{nextTurnId}\"}}"
			);
			GD.Print($"Turn finished for {tankId}. Next turn: {nextTurnId}");
		}
		catch (Exception e)
		{
			_animating = false;
			GD.PrintErr("Failed to parse command: " + e.Message);
		}
	}

	private async System.Threading.Tasks.Task ExecuteActionsAsync(Player entity, List<PlayerAction> actions)
	{
		foreach (var action in actions)
		{
			switch (action.Type)
			{
				case TankAction.MOVE:
					entity.MoveTurnBased(action.Direction);
					GameServer.Instance?.BroadcastMessage(
						$"{{\"event\": \"moved\", \"tankId\": \"{entity.Name}\", \"direction\": \"{action.Direction}\", \"pos_x\": {entity.Position.X}, \"pos_y\": {entity.Position.Y}}}"
					);
					if (entity.MoveTween != null && entity.MoveTween.IsRunning())
						await ToSignal(entity.MoveTween, Tween.SignalName.Finished);
					break;
				case TankAction.ROTATE:
					entity.RotateTurret(action.Degrees);
					GameServer.Instance?.BroadcastMessage(
						$"{{\"event\": \"rotated\", \"tankId\": \"{entity.Name}\", \"degrees\": {action.Degrees}}}"
					);
					if (entity.RotateTween != null && entity.RotateTween.IsRunning())
						await ToSignal(entity.RotateTween, Tween.SignalName.Finished);
					break;
				case TankAction.SHOOT:
					Bullet bullet = entity.Shoot();
					GameServer.Instance?.BroadcastMessage(
						$"{{\"event\": \"shot\", \"tankId\": \"{entity.Name}\"}}"
					);
					if (bullet != null)
						await ToSignal(bullet, Bullet.SignalName.BulletFinished);
					break;
			}
		}
	}
}
