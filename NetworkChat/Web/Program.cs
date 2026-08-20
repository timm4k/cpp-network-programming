using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Core.Models;
using Core.Services;

JsonSerializerOptions jsonOpts = new()
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Debug);
WebApplication app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    AllowedOrigins = { "*" },
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

ChatState state = new();
ConcurrentDictionary<string, WebSocket> connections = new();
ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> userConns = new(StringComparer.OrdinalIgnoreCase);

ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Chat");

app.UseStaticFiles();

app.MapGet("/api/rooms", () => state.Rooms.GetAllRoomNames());
app.MapGet("/api/users", () => state.Auth.GetAllLogins());

app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    WebSocket ws = await context.WebSockets.AcceptWebSocketAsync();
    string connId = Guid.NewGuid().ToString("N")[..8];
    connections[connId] = ws;
    string? currentUser = null;

    logger.LogInformation("WS OPEN {Id}", connId);

    byte[] buffer = new byte[65536];

    try
    {
        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;

            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            logger.LogInformation("WS RECV {Id}: {Json}", connId, json);

            ChatMessage? message = JsonSerializer.Deserialize<ChatMessage>(json, jsonOpts);
            if (message == null) continue;

            logger.LogInformation("MSG type={Type} sender={Sender}", message.Type, message.Sender);

            switch (message.Type)
            {
                case MessageType.Login:
                {
                    string[] parts = message.Content.Split('|');
                    if (parts.Length != 2)
                    {
                        await SendAsync(ws, new ChatMessage { Type = MessageType.Error, Content = "Invalid format" });
                        break;
                    }
                    var res = state.Auth.Login(parts[0], parts[1]);
                    if (!res.success)
                    {
                        await SendAsync(ws, new ChatMessage { Type = MessageType.Error, Content = res.error });
                        break;
                    }

                    currentUser = res.user!.Login;
                    state.Rooms.JoinRoom(RoomService.DefaultRoom, currentUser);
                    userConns.GetOrAdd(currentUser, _ => new())[connId] = 0;

                    await SendAsync(ws, new ChatMessage
                    {
                        Type = MessageType.Login,
                        Sender = "server",
                        Content = currentUser,
                        Target = res.user.IsAdmin ? "admin" : "user"
                    });

                    List<ChatMessage> history = state.Messages.GetHistory(RoomService.DefaultRoom);
                    foreach (var h in history)
                        await SendAsync(ws, h);

                    await BroadcastAsync(new ChatMessage { Type = MessageType.System, Sender = "server", Content = currentUser + " joined" });
                    await BroadcastListAsync(MessageType.UserList, state.Auth.GetAllLogins());
                    break;
                }

                case MessageType.Register:
                {
                    string[] parts = message.Content.Split('|');
                    if (parts.Length != 2)
                    {
                        await SendAsync(ws, new ChatMessage { Type = MessageType.Error, Content = "Invalid format" });
                        break;
                    }
                    var res = state.Auth.Register(parts[0], parts[1]);
                    await SendAsync(ws, new ChatMessage
                    {
                        Type = res.success ? MessageType.Register : MessageType.Error,
                        Sender = "server",
                        Content = res.success ? "ok" : res.error
                    });
                    break;
                }

                case MessageType.Chat:
                {
                    message = state.ProcessMessage(message);
                    message.Room ??= RoomService.DefaultRoom;
                    if (message.Type == MessageType.Chat)
                        state.Messages.Add(message);
                    if (state.Rooms.GetRoom(message.Room) is Room room)
                    {
                        foreach (string u in room.GetUsers())
                            await SendToUserAsync(u, message);
                    }
                    if (message.IsCensored)
                        await SendToUserAsync("admin", new ChatMessage { Type = MessageType.AdminCensorAlert, Sender = "server", Content = $"{message.Sender}: {message.Content}" });
                    break;
                }

                case MessageType.Private:
                {
                    if (message.Target == null || message.Sender == null) break;
                    string target = message.Target;
                    string sender = message.Sender;
                    message = state.ProcessMessage(message);
                    if (message.Type == MessageType.Private)
                        state.Messages.AddPrivate(message);
                    await SendToUserAsync(target, message);
                    if (!string.Equals(sender, target, StringComparison.OrdinalIgnoreCase))
                        await SendToUserAsync(sender, message);
                    break;
                }

                case MessageType.RoomCreate:
                    if (state.Rooms.CreateRoom(message.Content) != null)
                    {
                        state.Rooms.JoinRoom(message.Content, message.Sender);
                        await BroadcastListAsync(MessageType.RoomList, state.Rooms.GetAllRoomNames());
                    }
                    break;

                case MessageType.RoomJoin:
                    state.Rooms.JoinRoom(message.Content, message.Sender);
                    List<ChatMessage> joinHistory = state.Messages.GetHistory(message.Content);
                    foreach (var h in joinHistory)
                        await SendAsync(ws, h);
                    break;

                case MessageType.RoomLeave:
                    state.Rooms.LeaveRoom(message.Content, message.Sender);
                    break;

                case MessageType.RoomList:
                    await SendAsync(ws, new ChatMessage { Type = MessageType.RoomList, Sender = "server", Content = string.Join(",", state.Rooms.GetAllRoomNames()) });
                    break;

                case MessageType.UserList:
                    await SendAsync(ws, new ChatMessage { Type = MessageType.UserList, Sender = "server", Content = string.Join(",", state.Auth.GetAllLogins()) });
                    break;

                case MessageType.AdminDeleteUser:
                {
                    if (message.Target == null) break;
                    if (userConns.TryGetValue(message.Target, out var targetConns))
                        foreach (var cid in targetConns.Keys)
                            if (connections.TryGetValue(cid, out WebSocket? tw))
                                await SendAsync(tw, new ChatMessage { Type = MessageType.Error, Sender = "server", Content = "You have been removed by admin" });

                    state.Auth.DeleteUser(message.Target);
                    state.Rooms.RemoveUserFromAllRooms(message.Target);
                    userConns.TryRemove(message.Target, out _);
                    await BroadcastListAsync(MessageType.UserList, state.Auth.GetAllLogins());
                    break;
                }

                case MessageType.AdminBanUser:
                {
                    if (message.Target == null) break;
                    string[] banParts = message.Content.Split('|');
                    if (banParts.Length >= 2 && int.TryParse(banParts[1], out int mins))
                    {
                        state.Auth.BanUser(message.Target, TimeSpan.FromMinutes(mins), banParts[0]);
                        if (userConns.TryGetValue(message.Target, out var banConns))
                            foreach (var cid in banConns.Keys)
                                if (connections.TryGetValue(cid, out WebSocket? bws))
                                    await SendAsync(bws, new ChatMessage { Type = MessageType.Error, Sender = "server", Content = $"You are banned for {mins} min: {banParts[0]}" });
                    }
                    await BroadcastListAsync(MessageType.UserList, state.Auth.GetAllLogins());
                    break;
                }
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "WS ERROR {Id}", connId);
    }

    if (currentUser != null)
    {
        state.Rooms.RemoveUserFromAllRooms(currentUser);
        if (userConns.TryGetValue(currentUser, out var uc))
        {
            uc.TryRemove(connId, out _);
            if (uc.IsEmpty) userConns.TryRemove(currentUser, out _);
        }
        await BroadcastAsync(new ChatMessage { Type = MessageType.System, Sender = "server", Content = currentUser + " left" });
        await BroadcastListAsync(MessageType.UserList, state.Auth.GetAllLogins());
    }

    connections.TryRemove(connId, out _);

    try
    {
        if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }
    catch { }
});

app.MapFallbackToFile("index.html");
app.Run();

async Task SendAsync(WebSocket ws, ChatMessage msg)
{
    if (ws.State != WebSocketState.Open) return;
    string json = JsonSerializer.Serialize(msg, jsonOpts);
    byte[] data = Encoding.UTF8.GetBytes(json);
    try { await ws.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None); } catch { }
}

async Task SendToUserAsync(string login, ChatMessage msg)
{
    if (!userConns.TryGetValue(login, out var conns)) return;
    foreach (var cid in conns.Keys)
    {
        if (connections.TryGetValue(cid, out WebSocket? ws))
            await SendAsync(ws, msg);
    }
}

async Task BroadcastAsync(ChatMessage msg)
{
    string json = JsonSerializer.Serialize(msg, jsonOpts);
    byte[] data = Encoding.UTF8.GetBytes(json);
    foreach (WebSocket ws in connections.Values)
    {
        if (ws.State == WebSocketState.Open)
            try { await ws.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None); } catch { }
    }
}

async Task BroadcastListAsync(MessageType type, List<string> items)
{
    await BroadcastAsync(new ChatMessage { Type = type, Sender = "server", Content = string.Join(",", items) });
}
