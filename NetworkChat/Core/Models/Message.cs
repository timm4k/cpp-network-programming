namespace Core.Models;

public enum MessageType
{
    Chat,
    Private,
    System,
    Login,
    Register,
    RoomCreate,
    RoomJoin,
    RoomLeave,
    RoomList,
    UserList,
    AdminDeleteUser,
    AdminBanUser,
    AdminCensorAlert,
    Error
}

public sealed class ChatMessage
{
    public MessageType Type { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Room { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsCensored { get; set; }
}
