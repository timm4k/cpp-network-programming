using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Core.Models;
using Core.Services;

namespace Server.Ws;

public sealed class WebSocketChatServer : IDisposable
{
    private readonly ChatHandler _handler = new();
    private readonly ConcurrentDictionary<string, Fleck.IWebSocketConnection> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _connToUser = new();
    private Fleck.WebSocketServer? _server;

    public ChatState State => _handler.State;
    public Action<string>? Log { get => _handler.Log!; set => _handler.Log = value; }

    public Task StartAsync(int port, X509Certificate2? cert = null)
    {
        string url = cert != null ? $"wss://0.0.0.0:{port}" : $"ws://0.0.0.0:{port}";
        _server = new Fleck.WebSocketServer(url);
        if (cert != null) _server.Certificate = cert;

        _server.Start(socket =>
        {
            socket.OnOpen = () => Log?.Invoke($"WS connection opened: {socket.ConnectionInfo.ClientIpAddress}");
            socket.OnClose = () => HandleDisconnect(socket);
            socket.OnMessage = msg => HandleMessageAsync(msg, socket);
        });

        Log?.Invoke($"WebSocket server started on port {port} (TLS: {cert != null})");
        return Task.CompletedTask;
    }

    private void HandleDisconnect(Fleck.IWebSocketConnection socket)
    {
        string connId = socket.ConnectionInfo.Id.ToString();
        if (_connToUser.TryGetValue(connId, out string? login))
        {
            _clients.TryRemove(login, out _);
            _connToUser.TryRemove(connId, out _);
            _handler.HandleDisconnect(login);

            BroadcastAsync(_handler.MakeSystemMessage($"{login} left the chat")).GetAwaiter().GetResult();
            BroadcastAsync(_handler.MakeListMessage(MessageType.UserList, _handler.State.Auth.GetAllLogins())).GetAwaiter().GetResult();
        }
    }

    private void HandleMessageAsync(string json, Fleck.IWebSocketConnection socket)
    {
        ChatMessage? message = JsonSerializer.Deserialize<ChatMessage>(json);
        if (message == null) return;

        _ = Task.Run(async () =>
        {
            switch (message.Type)
            {
                case MessageType.Login:
                    await HandleLoginAsync(message, socket);
                    break;
                case MessageType.Register:
                    await HandleRegisterAsync(message, socket);
                    break;
                case MessageType.RoomList:
                    await SendAsync(socket, _handler.MakeListMessage(MessageType.RoomList, _handler.State.Rooms.GetAllRoomNames()));
                    break;
                case MessageType.UserList:
                    await SendAsync(socket, _handler.MakeListMessage(MessageType.UserList, _handler.State.Auth.GetAllLogins()));
                    break;
                default:
                    _handler.HandleMessage(message,
                        (user, msg) => SendToUserAsync(user, msg),
                        msg => BroadcastAsync(msg));
                    break;
            }
        });
    }

    private async Task HandleLoginAsync(ChatMessage message, Fleck.IWebSocketConnection socket)
    {
        var (success, error, user) = _handler.HandleLogin(message);
        if (!success)
        {
            await SendAsync(socket, new ChatMessage { Type = MessageType.Error, Content = error });
            return;
        }

        _clients[user!.Login] = socket;
        _connToUser[socket.ConnectionInfo.Id.ToString()] = user.Login;

        await SendAsync(socket, _handler.MakeLoginResponse(user));
        await BroadcastAsync(_handler.MakeSystemMessage($"{user.Login} joined the chat"));
        await BroadcastAsync(_handler.MakeListMessage(MessageType.UserList, _handler.State.Auth.GetAllLogins()));
        await BroadcastAsync(_handler.MakeListMessage(MessageType.RoomList, _handler.State.Rooms.GetAllRoomNames()));
    }

    private async Task HandleRegisterAsync(ChatMessage message, Fleck.IWebSocketConnection socket)
    {
        var (success, error, user) = _handler.HandleRegister(message);
        await SendAsync(socket, new ChatMessage
        {
            Type = success ? MessageType.Register : MessageType.Error,
            Sender = "server",
            Content = success ? "ok" : error
        });
    }

    private async Task SendToUserAsync(string login, ChatMessage message)
    {
        if (_clients.TryGetValue(login, out Fleck.IWebSocketConnection? conn))
            await SendAsync(conn, message);
    }

    private async Task BroadcastAsync(ChatMessage message)
    {
        string json = JsonSerializer.Serialize(message);
        foreach (Fleck.IWebSocketConnection conn in _clients.Values)
        {
            try { await conn.Send(json); } catch { }
        }
    }

    private async Task SendAsync(Fleck.IWebSocketConnection conn, ChatMessage message)
    {
        string json = JsonSerializer.Serialize(message);
        await conn.Send(json);
    }

    public void Dispose()
    {
        _server?.Dispose();
    }
}
