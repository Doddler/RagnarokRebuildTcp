using System.Text.Json;
using RoBotClient.Bot.Behavior;

namespace RoBotClient.Bot.Manager;

/// <summary>Per-character <see cref="BotMemory"/> in one JSON file (<c>bot-memory.json</c>), so map bans, death/flee
/// counters, monster avoidance and kill timings survive a dashboard restart instead of resetting every login.
/// Rewritten on every change; changes are a few per minute (kills, deaths, flees).</summary>
public sealed class BotMemoryStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, BotMemory> _memories = new(StringComparer.OrdinalIgnoreCase);

    public BotMemoryStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? Path.Combine(AppContext.BaseDirectory, "bot-memory.json") : path;
        try
        {
            if (File.Exists(_path) && JsonSerializer.Deserialize<Dictionary<string, BotMemory>>(File.ReadAllText(_path)) is { } raw)
                _memories = new(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch { /* corrupted → start fresh */ }
    }

    public BotMemory? Get(string characterName)
    {
        lock (_lock) return _memories.GetValueOrDefault(characterName);
    }

    public void Save(string characterName, BotMemory memory)
    {
        lock (_lock)
        {
            _memories[characterName] = memory;
            try
            {
                // Write-then-rename so a crash mid-write can't leave a truncated file.
                File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(_memories));
                File.Move(_path + ".tmp", _path, overwrite: true);
            }
            catch { }
        }
    }
}
