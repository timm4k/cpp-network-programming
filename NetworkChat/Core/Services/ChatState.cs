using Core.Models;

namespace Core.Services;

public sealed class ChatState
{
    public AuthService Auth { get; } = new();
    public CensorService Censor { get; } = new();
    public RoomService Rooms { get; } = new();
    public MessageStore Messages { get; } = new();

    public ChatMessage ProcessMessage(ChatMessage message)
    {
        if (message.Type == MessageType.Chat || message.Type == MessageType.Private)
        {
            User? sender = Auth.GetUser(message.Sender);
            if (sender != null && sender.IsBanned && message.Sender != "admin")
            {
                if (sender.BanExpiry.HasValue && sender.BanExpiry <= DateTime.UtcNow)
                {
                    sender.IsBanned = false;
                    sender.BanExpiry = null;
                    sender.BanReason = null;
                }
                else
                {
                    return new ChatMessage
                    {
                        Type = MessageType.Error,
                        Sender = "server",
                        Content = "You are banned"
                    };
                }
            }

            (string filtered, bool wasCensored) = Censor.Filter(message.Content);
            if (wasCensored)
            {
                message.Content = filtered;
                message.IsCensored = true;
            }
        }

        return message;
    }
}
