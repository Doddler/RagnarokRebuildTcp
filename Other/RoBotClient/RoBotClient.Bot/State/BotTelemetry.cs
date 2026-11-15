namespace RoBotClient.Bot.State;

// DamageDealt (=9) + DamageReceived (=10) + Disconnect (=11) are appended at the END so old persisted
// telemetry files keep deserializing — NEVER reorder these.
public enum TelemetryEventType { Kill, Death, MapChange, Loot, Sold, Bought, UsedItem, LevelUp, SkillCast, DamageDealt, DamageReceived, Disconnect }

/// <summary>One bot event, stamped with the bot's level at the time so stale (low-level) data can be aged out.</summary>
public sealed record TelemetryEvent(DateTime Time, int Level, TelemetryEventType Type, string Detail, int Value = 0);

/// <summary>
/// Per-bot, level-stamped event log (kills, deaths, map changes, items looted/sold/used, level-ups).
/// Feeds the dashboard and the MCP agent; the level stamp lets a consumer ignore stale low-level data.
/// Thread-safe: the session writes from its receive loop while the UI/agent reads.
/// </summary>
public sealed class BotTelemetry
{
    private const int MaxEvents = 5000;
    private readonly object _lock = new();
    private readonly List<TelemetryEvent> _events = new();
    private bool _dirty;

    public void Record(TelemetryEventType type, int level, string detail = "", int value = 0)
    {
        lock (_lock)
        {
            _events.Add(new TelemetryEvent(DateTime.UtcNow, level, type, detail, value));
            if (_events.Count > MaxEvents) _events.RemoveRange(0, _events.Count - MaxEvents);
            _dirty = true;
        }
    }

    /// <summary>Replace the in-memory log with persisted events (capped to MaxEvents). Called once on bot
    /// spawn so the agent's historical context survives a stop+respawn / dashboard restart.</summary>
    public void Load(IEnumerable<TelemetryEvent> events)
    {
        lock (_lock)
        {
            _events.Clear();
            _events.AddRange(events);
            if (_events.Count > MaxEvents) _events.RemoveRange(0, _events.Count - MaxEvents);
            _dirty = false; // freshly loaded = matches disk
        }
    }

    /// <summary>Snapshot the current event list for persistence (deep copy, safe to write to disk while the
    /// bot keeps running).</summary>
    public List<TelemetryEvent> Snapshot()
    {
        lock (_lock) return new List<TelemetryEvent>(_events);
    }

    /// <summary>Return true exactly once between Record calls, so a periodic flush can skip idle bots.</summary>
    public bool TakeDirty()
    {
        lock (_lock)
        {
            if (!_dirty) return false;
            _dirty = false;
            return true;
        }
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
