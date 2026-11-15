using System.Text.Json;
using RoBotClient.Bot.Behavior;

namespace RoBotClient.Bot.Manager;

/// <summary>
/// Per-character persistence for <see cref="BotBehaviorConfig"/>. Lets the user's <c>configure_bot</c> /
/// dashboard-Apply changes survive a bot stop+respawn, a dashboard restart, or a server bounce. Keyed by the
/// full character name (e.g. "[BOT] BotZero"); a freshly-created character takes its in-code defaults on
/// first spawn, gets saved immediately, and any subsequent reconnect to that character rehydrates from disk.
/// </summary>
public sealed class BotConfigStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, BotBehaviorConfig> _configs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        IncludeFields = true, // BotBehaviorConfig uses plain public fields, same as the rest of the shared schema
    };

    public BotConfigStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(AppContext.BaseDirectory, "bot-configs.json")
            : path;
        Load();
    }

    /// <summary>Read the saved config for <paramref name="characterName"/>, or null if nothing's saved.
    /// Returns a deep copy so callers can't accidentally mutate the cache.</summary>
    public BotBehaviorConfig? Get(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName)) return null;
        lock (_lock)
        {
            if (!_configs.TryGetValue(characterName, out var src)) return null;
            var copy = new BotBehaviorConfig();
            copy.CopyFrom(src);
            return copy;
        }
    }

    /// <summary>Persist <paramref name="config"/> under <paramref name="characterName"/>. The store keeps a
    /// deep copy so subsequent runtime mutations on the live config don't silently rewrite the file until
    /// the next Set call.</summary>
    public void Set(string characterName, BotBehaviorConfig config)
    {
        if (string.IsNullOrWhiteSpace(characterName) || config == null) return;
        var snapshot = new BotBehaviorConfig();
        snapshot.CopyFrom(config);
        lock (_lock)
        {
            _configs[characterName] = snapshot;
            Save();
        }
    }

    public IReadOnlyList<string> List()
    {
        lock (_lock) return _configs.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, BotBehaviorConfig>>(json, JsonOpts);
            if (raw != null)
                _configs = new Dictionary<string, BotBehaviorConfig>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch { /* corrupted/old-schema file → start fresh; next Save rewrites it */ }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_configs, JsonOpts));
        }
        catch { /* disk errors swallowed — next Set will retry */ }
    }
}
