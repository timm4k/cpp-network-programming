using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Models;

namespace Core.Services;

public sealed class AuthService
{
    private readonly ConcurrentDictionary<string, User> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;

    public AuthService(string? dataDir = null)
    {
        _path = Path.Combine(dataDir ?? Path.Combine(AppContext.BaseDirectory, "data"), "users.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        Load();
        if (!_users.ContainsKey("admin"))
        {
            _users["admin"] = new User { Login = "admin", PasswordHash = HashPassword("admin"), IsAdmin = true };
            Save();
        }
    }

    public string HashPassword(string password)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public (bool success, string error, User? user) Register(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Length < 2)
            return (false, "Login must be at least 2 characters", null);

        if (string.IsNullOrWhiteSpace(password) || password.Length < 3)
            return (false, "Password must be at least 3 characters", null);

        if (_users.ContainsKey(login))
            return (false, "Login already taken", null);

        User user = new()
        {
            Login = login,
            PasswordHash = HashPassword(password)
        };

        _users[login] = user;
        Save();
        return (true, string.Empty, user);
    }

    public (bool success, string error, User? user) Login(string login, string password)
    {
        if (!_users.TryGetValue(login, out User? user))
            return (false, "User not found", null);

        if (user.PasswordHash != HashPassword(password))
            return (false, "Wrong password", null);

        if (user.IsBanned)
        {
            if (user.BanExpiry.HasValue && user.BanExpiry > DateTime.UtcNow)
                return (false, $"Banned until {user.BanExpiry:HH:mm:ss}: {user.BanReason}", null);

            if (user.BanExpiry.HasValue && user.BanExpiry <= DateTime.UtcNow)
            {
                user.IsBanned = false;
                user.BanExpiry = null;
                user.BanReason = null;
            }
            else if (!user.BanExpiry.HasValue)
            {
                return (false, $"Banned permanently: {user.BanReason}", null);
            }
        }

        return (true, string.Empty, user);
    }

    public User? GetUser(string login)
    {
        _users.TryGetValue(login, out User? user);
        return user;
    }

    public List<string> GetAllLogins() => _users.Keys.ToList();

    public bool DeleteUser(string login)
    {
        if (login.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return false;
        bool removed = _users.TryRemove(login, out _);
        if (removed) Save();
        return removed;
    }

    public bool BanUser(string login, TimeSpan duration, string reason)
    {
        if (!_users.TryGetValue(login, out User? user))
            return false;

        user.IsBanned = true;
        user.BanExpiry = DateTime.UtcNow + duration;
        user.BanReason = reason;
        Save();
        return true;
    }

    public bool UnbanUser(string login)
    {
        if (!_users.TryGetValue(login, out User? user))
            return false;

        user.IsBanned = false;
        user.BanExpiry = null;
        user.BanReason = null;
        Save();
        return true;
    }

    private void Save()
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_path, JsonSerializer.Serialize(_users.Values.ToList(), opts));
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            string json = File.ReadAllText(_path);
            var list = JsonSerializer.Deserialize<List<User>>(json) ?? new();
            foreach (var u in list) _users[u.Login] = u;
        }
        catch { }
    }
}
