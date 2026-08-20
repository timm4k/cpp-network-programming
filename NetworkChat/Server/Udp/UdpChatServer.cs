using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Core.Models;
using Core.Services;

namespace Server.Udp;

public sealed class UdpChatServer : IDisposable
{
    private readonly ChatHandler _handler = new();
    private readonly UdpClient _udp;
    private readonly ConcurrentDictionary<string, IPEndPoint> _clients = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;

    public ChatState State => _handler.State;
    public Action<string>? Log { get => _handler.Log!; set => _handler.Log = value; }

    public UdpChatServer(int port)
    {
        _udp = new UdpClient(port);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _handler.Log?.Invoke($"UDP server started on port {((IPEndPoint)_udp.Client.LocalEndPoint!).Port}");

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await _udp.ReceiveAsync(_cts.Token);
                string json = Encoding.UTF8.GetString(result.Buffer);
                ChatMessage? message = JsonSerializer.Deserialize<ChatMessage>(json);
                if (message != null)
                {
                    _clients[message.Sender] = result.RemoteEndPoint;
                    _ = Task.Run(() => HandleMessageAsync(message, result.RemoteEndPoint));
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log?.Invoke($"Error: {ex.Message}"); }
        }
    }

    private async Task HandleMessageAsync(ChatMessage message, IPEndPoint sender)
    {
        switch (message.Type)
        {
            case MessageType.Login:
                await HandleLoginAsync(message, sender);
                break;
            case MessageType.Register:
                await HandleRegisterAsync(message, sender);
                break;
            case MessageType.RoomList:
                await SendAsync(sender, _handler.MakeListMessage(MessageType.RoomList, _handler.State.Rooms.GetAllRoomNames()));
                break;
            case MessageType.UserList:
                await SendAsync(sender, _handler.MakeListMessage(MessageType.UserList, _handler.State.Auth.GetAllLogins()));
                break;
            default:
                _handler.HandleMessage(message,
                    (user, msg) => SendToUserAsync(user, msg),
                    msg => BroadcastAsync(msg));
                break;
        }
    }

    private async Task HandleLoginAsync(ChatMessage message, IPEndPoint sender)
    {
        var (success, error, user) = _handler.HandleLogin(message);
        if (!success)
        {
            await SendAsync(sender, new ChatMessage { Type = MessageType.Error, Content = error });
            return;
        }

        await SendAsync(sender, _handler.MakeLoginResponse(user!));
        await BroadcastAsync(_handler.MakeSystemMessage($"{user!.Login} joined the chat"));
        await BroadcastAsync(_handler.MakeListMessage(MessageType.UserList, _handler.State.Auth.GetAllLogins()));
        await BroadcastAsync(_handler.MakeListMessage(MessageType.RoomList, _handler.State.Rooms.GetAllRoomNames()));
    }

    private async Task HandleRegisterAsync(ChatMessage message, IPEndPoint sender)
    {
        var (success, error, user) = _handler.HandleRegister(message);
        await SendAsync(sender, new ChatMessage
        {
            Type = success ? MessageType.Register : MessageType.Error,
            Sender = "server",
            Content = success ? "ok" : error
        });
    }

    private async Task SendToUserAsync(string login, ChatMessage message)
    {
        if (_clients.TryGetValue(login, out IPEndPoint? ep))
            await SendAsync(ep, message);
    }

    private async Task BroadcastAsync(ChatMessage message)
    {
        string json = JsonSerializer.Serialize(message);
        byte[] data = Encoding.UTF8.GetBytes(json);
        foreach (IPEndPoint ep in _clients.Values)
        {
            try { await _udp.SendAsync(data, data.Length, ep); } catch { }
        }
    }

    private async Task SendAsync(IPEndPoint ep, ChatMessage message)
    {
        string json = JsonSerializer.Serialize(message);
        byte[] data = Encoding.UTF8.GetBytes(json);
        await _udp.SendAsync(data, data.Length, ep);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _udp.Dispose();
    }
}
