namespace Core.Models;

public sealed class Room
{
    public string Name { get; init; } = string.Empty;
    private readonly object _lock = new();
    private readonly HashSet<string> _users = new(StringComparer.OrdinalIgnoreCase);

    public Room(string name)
    {
        Name = name;
    }

    public void AddUser(string login) { lock (_lock) _users.Add(login); }
    public bool RemoveUser(string login) { lock (_lock) return _users.Remove(login); }
    public bool ContainsUser(string login) { lock (_lock) return _users.Contains(login); }
    public List<string> GetUsers() { lock (_lock) return _users.ToList(); }
    public int Count { get { lock (_lock) return _users.Count; } }
}
