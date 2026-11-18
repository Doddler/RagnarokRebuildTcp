namespace RoBotClient.Bot.State;

// DamageDealt (=9) + DamageReceived (=10) + Disconnect (=11) are appended at the END so old persisted
// telemetry files keep deserializing — NEVER reorder these.
public enum TelemetryEventType { Kill, Death, MapChange, Loot, Sold, Bought, UsedItem, LevelUp, SkillCast, DamageDealt, DamageReceived, Disconnect }

/// <summary>One bot event, stamped with the bot's level at the time so stale (low-level) data can be aged out.</summary>
/// <remarks>Zeny: zeny received (Sold) for this event; 0 on events logged before it was recorded.</remarks>
public sealed record TelemetryEvent(DateTime Time, int Level, TelemetryEventType Type, string Detail, int Value = 0, int Zeny = 0);

/// <summary>
/// Per-bot, level-stamped event log (kills, deaths, map changes, items looted/sold/used, level-ups).
/// Feeds the dashboard and the MCP agent; the level stamp lets a consumer ignore stale low-level data.
/// Two tiers: history events (economy, progression, maps) are kept forever and persisted append-only;
/// rolling events (per-hit damage, skill casts) go stale as the bot levels, so only the newest MaxRolling are kept.
/// Thread-safe: the session writes from its receive loop while the UI/agent reads.
/// </summary>
public sealed class BotTelemetry
{
    public const int MaxRolling = 4000;
    private const int TrimSlack = 500; // trim in batches so a damage flood doesn't scan the list on every hit
    public static bool IsRolling(TelemetryEventType t) =>
        t is TelemetryEventType.DamageDealt or TelemetryEventType.DamageReceived or TelemetryEventType.SkillCast;

    private readonly object _lock = new();
    private readonly List<TelemetryEvent> _events = new();          // both tiers, oldest first
    private readonly List<TelemetryEvent> _unsavedHistory = new();  // history events not yet appended to disk
    private int _rolling;
    private bool _rollingDirty;

    public void Record(TelemetryEventType type, int level, string detail = "", int value = 0, int zeny = 0)
    {
        lock (_lock)
        {
            var e = new TelemetryEvent(DateTime.UtcNow, level, type, detail, value, zeny);
            _events.Add(e);
            if (!IsRolling(type)) { _unsavedHistory.Add(e); return; }
            _rolling++;
            _rollingDirty = true;
            if (_rolling > MaxRolling + TrimSlack) TrimRolling();
        }
    }

    /// <summary>Merge persisted events in front of anything recorded since spawn. Called once on connect so
    /// the history survives a stop+respawn / dashboard restart; loaded events already match disk.</summary>
    public void Load(IEnumerable<TelemetryEvent> events)
    {
        lock (_lock)
        {
            var merged = events.Concat(_events).OrderBy(e => e.Time).ToList();
            _events.Clear();
            _events.AddRange(merged);
            _rolling = _events.Count(e => IsRolling(e.Type));
            TrimRolling();
        }
    }

    /// <summary>What a flush must write: history events since the last call (to append) and, when rolling events
    /// changed, the whole rolling window (to overwrite). Each history event is handed out exactly once.</summary>
    public (List<TelemetryEvent> NewHistory, List<TelemetryEvent>? Rolling) TakeUnsaved()
    {
        lock (_lock)
        {
            var history = new List<TelemetryEvent>(_unsavedHistory);
            _unsavedHistory.Clear();
            var rolling = _rollingDirty ? _events.Where(e => IsRolling(e.Type)).ToList() : null;
            _rollingDirty = false;
            return (history, rolling);
        }
    }

    // Keep the newest MaxRolling rolling events. Caller holds _lock.
    private void TrimRolling()
    {
        if (_rolling <= MaxRolling) return;
        var drop = _rolling - MaxRolling;
        _events.RemoveAll(e => IsRolling(e.Type) && drop-- > 0); // RemoveAll visits in order: drops the oldest
        _rolling = MaxRolling;
    }

    /// <summary>Snapshot the current event list for persistence (deep copy, safe to write to disk while the
    /// bot keeps running).</summary>
    public List<TelemetryEvent> Snapshot()
    {
        lock (_lock) return new List<TelemetryEvent>(_events);
    }

    public List<TelemetryEvent> Recent(int minLevel = 0)
    {
        lock (_lock) return _events.Where(e => e.Level >= minLevel).ToList();
    }

    public int Count(TelemetryEventType type, int minLevel = 0)
    {
        lock (_lock) return _events.Count(e => e.Type == type && e.Level >= minLevel);
    }

    /// <summary>Sum the <see cref="TelemetryEvent.Value"/> across all events of <paramref name="type"/>.
    /// Used for damage-dealt / damage-received totals where each event's Value is the per-hit number.</summary>
    public long SumValue(TelemetryEventType type, int minLevel = 0)
    {
        lock (_lock)
        {
            long total = 0;
            foreach (var e in _events)
                if (e.Type == type && e.Level >= minLevel) total += e.Value;
            return total;
        }
    }

    /// <summary>Totals grouped by Detail (e.g. kills per monster), summing Value (or 1 if Value is 0).</summary>
    public Dictionary<string, int> CountByDetail(TelemetryEventType type, int minLevel = 0)
    {
        lock (_lock)
            return _events
                .Where(e => e.Type == type && e.Level >= minLevel && e.Detail.Length > 0)
                .GroupBy(e => e.Detail)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Value > 0 ? e.Value : 1));
    }

    /// <summary>Approximate time spent on each map, derived from consecutive MapChange events.</summary>
    public Dictionary<string, TimeSpan> TimeByMap()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
            TelemetryEvent? prev = null;
            foreach (var e in _events)
            {
                if (e.Type != TelemetryEventType.MapChange) continue;
                if (prev != null)
                    result[prev.Detail] = result.GetValueOrDefault(prev.Detail) + (e.Time - prev.Time);
                prev = e;
            }
            if (prev != null)
                result[prev.Detail] = result.GetValueOrDefault(prev.Detail) + (DateTime.UtcNow - prev.Time);
            return result;
        }
    }
}
