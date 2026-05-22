using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public partial class GameServer : Node
{
	private HttpListener _listener;
	private bool _isRunning = false;
	private List<WebSocket> _clients = new List<WebSocket>();
	private object _clientsLock = new object();

	public static ConcurrentQueue<CommandData> CommandQueue = new ConcurrentQueue<CommandData>();
	public static Dictionary<WebSocket, string> ClientTankIds = new Dictionary<WebSocket, string>();
	private static readonly object _tankIdsLock = new object();
	private const int MaxQueueSize = 20;

	public class CommandData
	{
		public WebSocket Client { get; set; }
		public string Message { get; set; }
	}

	public static void RegisterTankId(WebSocket client, string tankId)
	{
		lock (_tankIdsLock)
			ClientTankIds[client] = tankId;
	}

	public static GameServer Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		StartServer();
	}

	private void StartServer()
	{
		try
		{
			_listener = new HttpListener();
			_listener.Prefixes.Add("http://+:8080/");
			_listener.Start();
			_isRunning = true;

			Task.Run(ListenLoop);
			GD.Print("WebSocket Server started on ws://0.0.0.0:8080/ (all interfaces)");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to start server: {e.Message}");
		}
	}

	private async Task ListenLoop()
	{
		while (_isRunning)
		{
			try
			{
				HttpListenerContext context = await _listener.GetContextAsync();

				if (context.Request.IsWebSocketRequest)
				{
					ProcessWebSocketRequest(context);
				}
				else
				{
					HandleRestRequest(context);
				}
			}
			catch (Exception e)
			{
				if (_isRunning)
					GD.PrintErr($"Server error: {e.Message}");
			}
		}
	}

	private async void ProcessWebSocketRequest(HttpListenerContext context)
	{
		WebSocketContext wsContext = null;
		try
		{
			wsContext = await context.AcceptWebSocketAsync(null);
			WebSocket webSocket = wsContext.WebSocket;

			lock (_clientsLock)
			{
				_clients.Add(webSocket);
			}
			GD.Print("Client connected!");

			byte[] buffer = new byte[8192];

			while (webSocket.State == WebSocketState.Open)
			{
				// Accumulate frames until EndOfMessage to handle large messages
				var messageBytes = new System.IO.MemoryStream();
				WebSocketReceiveResult result;
				do
				{
					result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
					if (result.MessageType != WebSocketMessageType.Close)
						messageBytes.Write(buffer, 0, result.Count);
				}
				while (!result.EndOfMessage);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
				}
				else if (CommandQueue.Count < MaxQueueSize)
				{
					string message = Encoding.UTF8.GetString(messageBytes.ToArray());
					CommandQueue.Enqueue(new CommandData { Client = webSocket, Message = message });
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"WebSocket error: {e.Message}");
		}
		finally
		{
			if (wsContext != null)
			{
				lock (_clientsLock)
					_clients.Remove(wsContext.WebSocket);
				lock (_tankIdsLock)
					ClientTankIds.Remove(wsContext.WebSocket);
				GD.Print("Client disconnected.");
			}
		}
	}

	public async Task DisconnectAllClients()
	{
		List<WebSocket> toClose;
		lock (_clientsLock)
		{
			toClose = new List<WebSocket>(_clients);
		}
		foreach (var client in toClose)
		{
			if (client.State == WebSocketState.Open)
			{
				try
				{
					await client.CloseAsync(
						WebSocketCloseStatus.NormalClosure,
						"Lobby reset",
						CancellationToken.None
					);
				}
				catch { }
			}
		}
	}

	public void BroadcastMessage(string message)
	{
		byte[] buffer = Encoding.UTF8.GetBytes(message);
		lock (_clientsLock)
		{
			foreach (var client in _clients)
			{
				if (client.State == WebSocketState.Open)
				{
					client.SendAsync(
						new ArraySegment<byte>(buffer),
						WebSocketMessageType.Text,
						true,
						CancellationToken.None
					);
				}
			}
		}
	}

	// ── REST endpoints ───────────────────────────────────────────────
	// GET /map         → grid dimensions + all walls
	// GET /players     → all player positions/turret angles
	// GET /player/{id} → single player state
	// GET /state       → game status, turn, round, scores
	// GET /constants   → physics constants needed for shooting math

	private void HandleRestRequest(HttpListenerContext ctx)
	{
		if (ctx.Request.HttpMethod != "GET")
		{
			ctx.Response.StatusCode = 405;
			ctx.Response.Close();
			return;
		}

		string path = ctx.Request.Url?.AbsolutePath ?? "/";

		string json;
		if (path == "/map")
			json = BuildMapJson();
		else if (path == "/players")
			json = BuildPlayersJson();
		else if (path.StartsWith("/player/"))
			json = BuildPlayerJson(path.Substring("/player/".Length));
		else if (path == "/state")
			json = BuildStateJson();
		else if (path == "/constants")
			json = BuildConstantsJson();
		else if (path == "/turn_time")
			json = BuildTurnTimeJson();
		else
		{
			ctx.Response.StatusCode = 404;
			ctx.Response.Close();
			return;
		}

		if (json == null)
		{
			ctx.Response.StatusCode = 404;
			ctx.Response.Close();
			return;
		}

		byte[] bytes = Encoding.UTF8.GetBytes(json);
		ctx.Response.StatusCode = 200;
		ctx.Response.ContentType = "application/json; charset=utf-8";
		ctx.Response.ContentLength64 = bytes.Length;
		ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
		ctx.Response.Close();
	}

	private static string BuildMapJson()
	{
		int gs = GlobalState.Instance?.GridSize ?? 128;
		var sb = new StringBuilder();
		sb.Append("{");
		sb.Append("\"gridSize\":" + gs);
		sb.Append(",\"walls\":[");
		bool first = true;
		foreach (var (pos, orientation) in GlobalState.WallRegistry)
		{
			if (!first) sb.Append(",");
			string o = orientation == 0 ? "HORIZONTAL" : "VERTICAL";
			sb.Append($"{{\"x\":{pos.X},\"y\":{pos.Y},\"orientation\":\"{o}\"}}");
			first = false;
		}
		sb.Append("]}");
		return sb.ToString();
	}

	private static string BuildPlayersJson()
	{
		var snap = GameManager.Instance?.GetSnapshot();
		if (snap == null) return "{\"players\":[]}";

		var sb = new StringBuilder();
		sb.Append("{\"players\":[");
		bool first = true;
		foreach (var p in snap.Players)
		{
			if (!first) sb.Append(",");
			AppendPlayerJson(sb, p);
			first = false;
		}
		sb.Append("]}");
		return sb.ToString();
	}

	private static string BuildPlayerJson(string tankId)
	{
		var snap = GameManager.Instance?.GetSnapshot();
		if (snap == null) return null;
		foreach (var p in snap.Players)
		{
			if (p.TankId == tankId)
			{
				var sb = new StringBuilder();
				AppendPlayerJson(sb, p);
				return sb.ToString();
			}
		}
		return null;
	}

	private static void AppendPlayerJson(StringBuilder sb, GameManager.PlayerSnapshot p)
	{
		sb.Append($"{{\"tankId\":\"{p.TankId}\"");
		sb.Append($",\"gridX\":{p.GridX},\"gridY\":{p.GridY}");
		sb.Append($",\"posX\":{p.PosX:F2},\"posY\":{p.PosY:F2}");
		sb.Append($",\"turretDegrees\":{p.TurretDegrees:F2}");
		sb.Append("}");
	}

	private static string BuildStateJson()
	{
		var snap = GameManager.Instance?.GetSnapshot();
		if (snap == null)
			return "{\"gameStarted\":false,\"gameOver\":false,\"onTurn\":\"\",\"round\":0,\"scores\":{}}";

		var sb = new StringBuilder();
		sb.Append("{");
		sb.Append($"\"gameStarted\":{(snap.GameStarted ? "true" : "false")}");
		sb.Append($",\"gameOver\":{(snap.GameOver ? "true" : "false")}");
		sb.Append($",\"onTurn\":\"{snap.OnTurn}\"");
		sb.Append($",\"round\":{snap.Round}");
		sb.Append(",\"scores\":{");
		bool first = true;
		foreach (var kv in snap.Scores)
		{
			if (!first) sb.Append(",");
			sb.Append($"\"{kv.Key}\":{kv.Value}");
			first = false;
		}
		sb.Append("}}");
		return sb.ToString();
	}

	private static string BuildConstantsJson()
	{
		int gs = GlobalState.Instance?.GridSize ?? 128;
		float tankBodyHalf  = gs * 0.25f;   // half of body collision (gs*0.5 / 2)
		float muzzleOffset  = gs * 0.42f;   // bullet spawn distance from tank center
		float bulletRadius  = gs * 0.08f;   // bullet collision radius

		return $"{{" +
			   $"\"gridSize\":{gs}" +
			   $",\"bulletSpeed\":600.0" +
			   $",\"bulletMaxBounces\":2" +
			   $",\"tankBodySize\":{gs * 0.5f:F2}" +
			   $",\"tankBodyHalfSize\":{tankBodyHalf:F2}" +
			   $",\"muzzleOffset\":{muzzleOffset:F2}" +
			   $",\"bulletRadius\":{bulletRadius:F2}" +
			   $"}}";
	}
	private static string BuildTurnTimeJson()
	{
		var gm = GameManager.Instance;
		double remaining = gm?.GetTurnTimeRemainingSeconds() ?? 0;
		var snap = gm?.GetSnapshot();
		string onTurn = snap?.OnTurn ?? "";
		return $"{{\"turnTimeRemainingSeconds\":{remaining:F2},\"onTurn\":\"{onTurn}\"}}";
	}
	// ─────────────────────────────────────────────────────────────────

	public override void _ExitTree()
	{
		_isRunning = false;
		_listener?.Stop();
	}
}
