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

	public class CommandData
	{
		public WebSocket Client { get; set; }
		public string Message { get; set; }
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
					context.Response.StatusCode = 400;
					context.Response.Close();
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

			byte[] buffer = new byte[1024];

			while (webSocket.State == WebSocketState.Open)
			{
				WebSocketReceiveResult result = await webSocket.ReceiveAsync(
					new ArraySegment<byte>(buffer),
					CancellationToken.None
				);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					await webSocket.CloseAsync(
						WebSocketCloseStatus.NormalClosure,
						"",
						CancellationToken.None
					);
				}
				else
				{
					string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
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
				{
					_clients.Remove(wsContext.WebSocket);
				}
				GD.Print("Client disconnected.");
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

	public override void _ExitTree()
	{
		_isRunning = false;
		_listener?.Stop();
	}
}
