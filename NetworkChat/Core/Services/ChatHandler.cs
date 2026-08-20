using Core.Models;

namespace Core.Services;

public sealed class ChatHandler
{
    public ChatState State { get; } = new();
    public Action<string>? Log;

    public void HandleMessage(ChatMessage message,
        Func<string, ChatMessage, Task> sendToUser, Func<ChatMessage, Task> broadcast)
    {
        switch (message.Type)
        {
            case MessageType.Chat:
                HandleChat(message, sendToUser, broadcast);
                break;
            case MessageType.Private:
                HandlePrivate(message, sendToUser);
                break;
            case MessageType.RoomCreate:
                HandleRoomCreate(message, broadcast);
                break;
            case MessageType.RoomJoin:
                HandleRoomJoin(message);
                break;
            case MessageType.RoomLeave:
                HandleRoomLeave(message);
                break;
            case MessageType.AdminDeleteUser:
                HandleDeleteUser(message, sendToUser, broadcast);
                break;
            case MessageType.AdminBanUser:
                HandleBanUser(message, sendToUser, broadcast);
                break;
        }
    }

    private void HandleChat(ChatMessage message, Func<string, ChatMessage, Task> sendToUser,
        Func<ChatMessage, Task> broadcast)
    {
        message = State.ProcessMessage(message);
        message.Room ??= RoomService.DefaultRoom;

        if (message.Type == MessageType.Chat)
            State.Messages.Add(message);

        if (State.Rooms.GetRoom(message.Room) is Room room)
            foreach (string u in room.GetUsers())
                sendToUser(u, message).GetAwaiter().GetResult();

        if (message.IsCensored)
        {
            Log?.Invoke($"CENSOR: {message.Sender}: {message.Content}");
            sendToUser("admin", new ChatMessage
            {
                Type = MessageType.AdminCensorAlert,
                Sender = "server",
                Content = $"{message.Sender}: {message.Content}"
            }).GetAwaiter().GetResult();
        }
    }

    private void HandlePrivate(ChatMessage message, Func<string, ChatMessage, Task> sendToUser)
    {
        message = State.ProcessMessage(message);

        if (message.Type == MessageType.Private)
            State.Messages.AddPrivate(message);

        if (message.Target != null)
        {
            sendToUser(message.Target, message).GetAwaiter().GetResult();
            if (!string.Equals(message.Sender, message.Target, StringComparison.OrdinalIgnoreCase))
                sendToUser(message.Sender, message).GetAwaiter().GetResult();
        }
    }

    private void HandleRoomCreate(ChatMessage message, Func<ChatMessage, Task> broadcast)
    {
        Room? room = State.Rooms.CreateRoom(message.Content);
        if (room == null) return;
        State.Rooms.JoinRoom(message.Content, message.Sender);
        Log?.Invoke($"Room '{message.Content}' created by {message.Sender}");
        broadcast(new ChatMessage { Type = MessageType.RoomList, Sender = "server",
            Content = string.Join(",", State.Rooms.GetAllRoomNames()) }).GetAwaiter().GetResult();
    }

    private void HandleRoomJoin(ChatMessage message)
    {
        State.Rooms.JoinRoom(message.Content, message.Sender);
        Log?.Invoke($"{message.Sender} joined room '{message.Content}'");
    }

    private void HandleRoomLeave(ChatMessage message)
    {
        State.Rooms.LeaveRoom(message.Content, message.Sender);
        Log?.Invoke($"{message.Sender} left room '{message.Content}'");
    }

    private void HandleDeleteUser(ChatMessage message, Func<string, ChatMessage, Task> sendToUser,
        Func<ChatMessage, Task> broadcast)
    {
        if (message.Target == null) return;

        sendToUser(message.Target, new ChatMessage
        {
            Type = MessageType.Error, Sender = "server", Content = "You have been removed by admin"
        }).GetAwaiter().GetResult();

        State.Auth.DeleteUser(message.Target);
        State.Rooms.RemoveUserFromAllRooms(message.Target);
        Log?.Invoke($"Admin deleted user: {message.Target}");
        broadcast(new ChatMessage { Type = MessageType.UserList, Sender = "server",
            Content = string.Join(",", State.Auth.GetAllLogins()) }).GetAwaiter().GetResult();
    }

    private void HandleBanUser(ChatMessage message, Func<string, ChatMessage, Task> sendToUser,
        Func<ChatMessage, Task> broadcast)
    {
        if (message.Target == null) return;

        string[] parts = message.Content.Split('|');
        if (parts.Length >= 2 && int.TryParse(parts[1], out int minutes))
        {
            State.Auth.BanUser(message.Target, TimeSpan.FromMinutes(minutes), parts[0]);
            Log?.Invoke($"Admin banned {message.Target} for {minutes} min: {parts[0]}");

            sendToUser(message.Target, new ChatMessage
            {
                Type = MessageType.Error, Sender = "server",
                Content = $"You are banned for {minutes} min: {parts[0]}"
            }).GetAwaiter().GetResult();
        }
        broadcast(new ChatMessage { Type = MessageType.UserList, Sender = "server",
            Content = string.Join(",", State.Auth.GetAllLogins()) }).GetAwaiter().GetResult();
    }

    public (bool success, string error, User? user) HandleLogin(ChatMessage message)
    {
        string[] parts = message.Content.Split('|');
        if (parts.Length != 2)
            return (false, "Invalid format", null);

        var result = State.Auth.Login(parts[0], parts[1]);
        if (!result.success || result.user == null) return result;

        State.Rooms.JoinRoom(RoomService.DefaultRoom, result.user.Login);
        Log?.Invoke($"{result.user.Login} logged in");
        return result;
    }

    public (bool success, string error, User? user) HandleRegister(ChatMessage message)
    {
        string[] parts = message.Content.Split('|');
        if (parts.Length != 2)
            return (false, "Invalid format", null);

        var result = State.Auth.Register(parts[0], parts[1]);
        if (result.success)
            Log?.Invoke($"{result.user!.Login} registered");
        return result;
    }

    public void HandleDisconnect(string login)
    {
        State.Rooms.RemoveUserFromAllRooms(login);
        Log?.Invoke($"{login} disconnected");
    }

    public ChatMessage MakeLoginResponse(User user) => new()
    {
        Type = MessageType.Login, Sender = "server",
        Content = user.Login, Target = user.IsAdmin ? "admin" : "user"
    };

    public ChatMessage MakeSystemMessage(string text) => new()
    {
        Type = MessageType.System, Sender = "server", Content = text
    };

    public ChatMessage MakeListMessage(MessageType type, List<string> items) => new()
    {
        Type = type, Sender = "server", Content = string.Join(",", items)
    };
}
