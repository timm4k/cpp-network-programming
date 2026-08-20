using System.Collections.Concurrent;
using System.Text.Json;
using Core.Models;

namespace Core.Services;

public sealed class MessageStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    private const int MaxPerRoom = 200;

    public MessageStore(string? dataDir = null)
    {
        _path = Path.Combine(dataDir ?? Path.Combine(AppContext.BaseDirectory, "data"), "messages.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        Load();
    }

    public void Add(ChatMessage msg)
    {
        if (msg.Type != MessageType.Chat && msg.Type != MessageType.Private) return;
        string room = msg.Room ?? RoomService.DefaultRoom;
        var list = _rooms.GetOrAdd(room, _ => new());
        lock (list)
        {
            list.Add(msg);
            while (list.Count > MaxPerRoom) list.RemoveAt(0);
        }
        Save();
    }

    public List<ChatMessage> GetHistory(string room, int count = 50)
    {
        if (!_rooms.TryGetValue(room, out var list)) return new();
        lock (list)
        {
            int take = Math.Min(count, list.Count);
            return list.Skip(list.Count - take).Take(take).ToList();
        }
    }

    public void AddPrivate(ChatMessage msg)
    {
        if (msg.Target == null) return;
        string key = PrivateKey(msg.Sender, msg.Target);
        ChatMessage stored = new()
        {
            Type = msg.Type,
            Sender = msg.Sender,
            Target = msg.Target,
            Content = msg.Content,
            Room = key,
            IsCensored = msg.IsCensored
        };
        Add(stored);
    }

    private static string PrivateKey(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0
            ? $"__pm__{a}__{b}" : $"__pm__{b}__{a}";

    private void Save()
    {
        try
        {
            var opts = new JsonSerializerOptions { WriteIndented = false };
            var data = _rooms.ToDictionary(kv => kv.Key, kv =>
            {
                lock (kv.Value) return kv.Value.ToList();
            });
            File.WriteAllText(_path, JsonSerializer.Serialize(data, opts));
        }
        catch { }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            string json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<ChatMessage>>>(json) ?? new();
            foreach (var kv in data)
                _rooms[kv.Key] = kv.Value;
        }
        catch { }
    }
}
