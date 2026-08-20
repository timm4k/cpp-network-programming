using System.Collections.Concurrent;
using System.Text.Json;
using Core.Models;

namespace Core.Services;

public sealed class RoomService
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    public const string DefaultRoom = "general";
    private static readonly string[] DefaultRooms = [DefaultRoom, "random", "games", "music", "dev"];

    public RoomService(string? dataDir = null)
    {
        _path = Path.Combine(dataDir ?? Path.Combine(AppContext.BaseDirectory, "data"), "rooms.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        Load();
        foreach (string name in DefaultRooms)
            _rooms.TryAdd(name, new Room(name));
        Save();
    }

    public Room? CreateRoom(string name)
    {
        if (_rooms.ContainsKey(name)) return null;
        Room room = new(name);
        _rooms[name] = room;
        Save();
        return room;
    }

    public bool DeleteRoom(string name)
    {
        if (name.Equals("general", StringComparison.OrdinalIgnoreCase))
            return false;
        bool removed = _rooms.TryRemove(name, out _);
        if (removed) Save();
        return removed;
    }

    public Room? GetRoom(string name)
    {
        _rooms.TryGetValue(name, out Room? room);
        return room;
    }

    public List<string> GetAllRoomNames() => _rooms.Keys.ToList();

    public void JoinRoom(string roomName, string userLogin)
    {
        if (_rooms.TryGetValue(roomName, out Room? room))
            room.AddUser(userLogin);
    }

    public void LeaveRoom(string roomName, string userLogin)
    {
        if (_rooms.TryGetValue(roomName, out Room? room))
            room.RemoveUser(userLogin);
    }

    public void RemoveUserFromAllRooms(string userLogin)
    {
        foreach (Room room in _rooms.Values)
            room.RemoveUser(userLogin);
    }

    private void Save()
    {
        try
        {
            var names = _rooms.Keys.ToList();
            File.WriteAllText(_path, JsonSerializer.Serialize(names, new JsonSerializerOptions { WriteIndented = false }));
        }
        catch { }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var names = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_path)) ?? new();
            foreach (string name in names)
                _rooms.TryAdd(name, new Room(name));
        }
        catch { }
    }
}
