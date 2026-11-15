using System.Text.Json;

namespace RoBotClient.Bot.Manager;

/// <summary>What we know about a bot's party membership locally — persisted so a reconnect can rehydrate
/// the MCP-visible <c>get_party</c> state without having to wait for the server to re-broadcast it.</summary>
public sealed class BotPartyState
{
    public bool InParty;
    public bool IsLeader;
    public string LeaderName = "";
}

/// <summary>
/// Persistent per-character party state. The server keeps the bot in its party across reconnects, but the
/// bot's local flags (InParty / IsPartyLeader / PartyLeaderName) reset to defaults on every fresh session —
/// so MCP <c>get_party</c> would lie about the bot's status until the user manually re-set it. This store
/// snapshots those flags to disk whenever they change and restores them on the next connect.
/// </summary>
public sealed class PartyStateStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, BotPartyState> _states = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    public PartyStateStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(AppContext.BaseDirectory, "bot-party-state.json")
            : path;
        Load();
    }

    public BotPartyState? Get(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName)) return null;
        lock (_lock)
        {
            if (!_states.TryGetValue(characterName, out var s)) return null;
            return new BotPartyState { InParty = s.InParty, IsLeader = s.IsLeader, LeaderName = s.LeaderName };
        }
    }

    public void Save(string characterName, BotPartyState state)
    {
        if (string.IsNullOrWhiteSpace(characterName) || state == null) return;
        lock (_lock)
        {
            _states[characterName] = new BotPartyState
            {
                InParty = state.InParty,
                IsLeader = state.IsLeader,
                LeaderName = state.LeaderName ?? "",
            };
            Write();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, BotPartyState>>(json, JsonOpts);
            if (raw != null)
                _states = new Dictionary<string, BotPartyState>(raw, StringComparer.OrdinalIgnoreCase);
        }
        catch { /* corrupted/missing → start fresh */ }
    }

    private void Write()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_states, JsonOpts));
        }
        catch { }
    }
}
