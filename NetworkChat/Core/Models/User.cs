namespace Core.Models;

public sealed class User
{
    public string Login { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
    public bool IsBanned { get; set; }
    public DateTime? BanExpiry { get; set; }
    public string? BanReason { get; set; }
}
