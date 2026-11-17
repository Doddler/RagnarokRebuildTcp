using System.Text.Json;
using RoBotClient.Bot.Behavior;

namespace RoBotClient.Bot.Manager;

/// <summary>Append-only map move audit, one JSON line per move under <c>bot-map-audit/&lt;character&gt;.jsonl</c>, never truncated.
/// Read on demand by the dashboard (moves are rare, a few per hour), so there's no cache.</summary>
public sealed class MapAuditStore
{
    private readonly string _dir;
    private readonly object _lock = new();

    public MapAuditStore(string? dir = null)
    {
        _dir = string.IsNullOrWhiteSpace(dir) ? Path.Combine(AppContext.BaseDirectory, "bot-map-audit") : dir;
        try { Directory.CreateDirectory(_dir); } catch { }
    }

    public void Append(string characterName, MapMoveAudit audit)
    {
        lock (_lock)
            try { File.AppendAllText(PathFor(characterName), JsonSerializer.Serialize(audit) + "\n"); } catch { }
    }

    /// <summary>One character's moves, oldest first.</summary>
    public List<MapMoveAudit> Get(string characterName) => Read(PathFor(characterName));

    /// <summary>Every character's moves, one list per character (CandidateRealization is one global factor, so it's judged on all bots).</summary>
    public List<List<MapMoveAudit>> All()
    {
        try { return Directory.GetFiles(_dir, "*.jsonl").Select(Read).ToList(); }
        catch { return new(); }
    }

    private List<MapMoveAudit> Read(string path)
    {
        var result = new List<MapMoveAudit>();
        lock (_lock)
            try
            {
                if (!File.Exists(path)) return result;
                foreach (var line in File.ReadLines(path))
                    try { if (JsonSerializer.Deserialize<MapMoveAudit>(line) is { } a) result.Add(a); } catch (JsonException) { } // a line cut short by a crash
            }
            catch { }
        return result;
    }

    private string PathFor(string characterName)
    {
        // Same filename sanitizing as BotProgressStore.
        var safe = new string((characterName ?? "").Where(c => char.IsLetterOrDigit(c) || " _-[]()".Contains(c)).ToArray()).Trim();
        if (string.IsNullOrEmpty(safe)) safe = "unnamed";
        return Path.Combine(_dir, safe + ".jsonl");
    }
}
