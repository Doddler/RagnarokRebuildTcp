using System.Text.Json;

namespace RoBotClient.Bot.Manager;

/// <summary>One point on a character's progression curve. TotalExp is lifetime base exp (from the level chart);
/// OwnExp and PartyExp are running totals, since tracking began, of exp from the bot's own kills and from party
/// shares. Their sum can differ from TotalExp's growth (exp before tracking, milestone events). On the bot's own
/// kills, Monster names the kill and TimeToKill is seconds from its first hit to the death and Attacks the swings plus damaging skill uses spent
/// on it and HpLost the damage it dealt the bot (all null otherwise). Party/Healer give the fight's context (in a
/// party; a party bot on the map can cast Heal; another player also hit the monster) and Pred* what the solo duel forecast predicted when engaging.</summary>
public sealed record ProgressSample(DateTime Time, double PlaytimeSeconds, long TotalExp, int Level,
    long OwnExp = 0, long PartyExp = 0, string? Monster = null, double? TimeToKill = null, int? Attacks = null, int? HpLost = null,
    bool? Party = null, bool? Healer = null, double? PredSeconds = null, int? PredAttacks = null, double? PredHpLost = null,
    bool? Helped = null, double? Fight = null, double? Search = null, double? Recover = null, double? Flee = null)
{
    /// <summary>Running totals of seconds fighting / searching / recovering / fleeing (null on samples from before they were tracked).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public RoBotClient.Bot.Behavior.ActivityTime? Activity => Fight is { } fight ? new(fight, Search ?? 0, Recover ?? 0, Flee ?? 0) : null;
}

/// <summary>
/// Persistent per-character progression history (playtime vs lifetime base exp) for the dashboard chart.
/// Kept apart from <see cref="BotTelemetryStore"/> because telemetry is a capped rolling window of events and
/// would drop the early part of the curve. One append-only JSON-lines file per character under
/// <c>bot-progress/</c>, appended on every exp update (~20 KB per hour of solo play); a sample is one short line, so
/// appending is cheap and history is never truncated.
/// </summary>
public sealed class BotProgressStore
{
    private readonly string _dir;
    private readonly object _lock = new();
    private readonly Dictionary<string, List<ProgressSample>> _cache = new(StringComparer.OrdinalIgnoreCase);
    // Most samples aren't kills; skip their null Monster/TimeToKill fields to keep lines short.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public BotProgressStore(string? dir = null)
    {
        _dir = string.IsNullOrWhiteSpace(dir)
            ? Path.Combine(AppContext.BaseDirectory, "bot-progress")
            : dir;
        try { Directory.CreateDirectory(_dir); } catch { }
    }

    public IReadOnlyList<ProgressSample> Get(string characterName)
    {
        lock (_lock)
            return GetOrLoad(characterName).ToList();
    }

    /// <summary>Current base exp rate: total-exp growth over the last <paramref name="windowMinutes"/> of playtime
    /// (offline time excluded, since samples carry playtime), per hour. Null until there's a minute of data.</summary>
    public double? ExpPerHour(string characterName, int windowMinutes = 15)
    {
        lock (_lock)
        {
            var samples = GetOrLoad(characterName);
            if (samples.Count < 2) return null;
            var last = samples[^1];
            var from = samples.LastOrDefault(p => p.PlaytimeSeconds <= last.PlaytimeSeconds - windowMinutes * 60) ?? samples[0];
            var hours = (last.PlaytimeSeconds - from.PlaytimeSeconds) / 3600;
            return hours < 1.0 / 60 ? null : (last.TotalExp - from.TotalExp) / hours;
        }
    }

    /// <summary>When this character last gained exp (its history file's write time), or null if it never did.</summary>
    public DateTime? LastPlayedUtc(string characterName)
    {
        var path = PathFor(characterName);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
    }

    public void Append(string characterName, ProgressSample sample)
    {
        lock (_lock)
        {
            GetOrLoad(characterName).Add(sample);
            try { File.AppendAllText(PathFor(characterName), JsonSerializer.Serialize(sample, JsonOpts) + "\n"); }
            catch { }
        }
    }

    private List<ProgressSample> GetOrLoad(string characterName)
    {
        if (_cache.TryGetValue(characterName, out var samples))
            return samples;

        samples = new List<ProgressSample>();
        try
        {
            var path = PathFor(characterName);
            if (File.Exists(path))
            {
                foreach (var line in File.ReadLines(path))
                {
                    // A line cut short by a crash mid-write is skipped rather than losing the whole file.
                    try { if (JsonSerializer.Deserialize<ProgressSample>(line) is { } s) samples.Add(s); }
                    catch (JsonException) { }
                }
            }
        }
        catch { }
        _cache[characterName] = samples;
        return samples;
    }

    private string PathFor(string characterName)
    {
        // Same filename sanitizing as BotTelemetryStore.
        var safe = new string((characterName ?? "").Where(c => char.IsLetterOrDigit(c) || " _-[]()".Contains(c)).ToArray()).Trim();
        if (string.IsNullOrEmpty(safe)) safe = "unnamed";
        return Path.Combine(_dir, safe + ".jsonl");
    }
}
