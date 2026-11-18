using System.Text.Json;
using System.Text.Json.Serialization;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Manager;

/// <summary>
/// Persistent per-character telemetry under <c>bot-telemetry/</c>, in two files per character:
/// <c>&lt;name&gt;.jsonl</c> holds history events, one per line, appended and never truncated (a crash costs at most a
/// half-written last line, skipped on load); <c>&lt;name&gt;.rolling.json</c> holds the capped rolling window
/// (damage, skill casts), rewritten through a temp file so a crash never leaves it half-written.
/// A legacy single-file <c>&lt;name&gt;.json</c> is split into these once and renamed to <c>.json.migrated</c>.
/// </summary>
public sealed class BotTelemetryStore
{
    private readonly string _dir;
    private readonly object _lock = new(); // the periodic flush and a bot's final flush can overlap
    // Value/Zeny are 0 on most events; skip them to keep lines short.
    private static readonly JsonSerializerOptions LineOpts = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

    public BotTelemetryStore(string? dir = null)
    {
        _dir = string.IsNullOrWhiteSpace(dir)
            ? Path.Combine(AppContext.BaseDirectory, "bot-telemetry")
            : dir;
        try { Directory.CreateDirectory(_dir); } catch { }
    }

    public List<TelemetryEvent>? Load(string characterName)
    {
        lock (_lock)
        {
            var (history, rolling, legacy) = PathsFor(characterName);
            MigrateLegacy(history, rolling, legacy);
            var events = new List<TelemetryEvent>();
            try
            {
                if (File.Exists(history))
                    foreach (var line in File.ReadLines(history))
                    {
                        try { if (JsonSerializer.Deserialize<TelemetryEvent>(line) is { } e) events.Add(e); }
                        catch (JsonException) { } // a line cut short by a crash mid-write
                    }
            }
            catch { }
            try
            {
                if (File.Exists(rolling) && JsonSerializer.Deserialize<List<TelemetryEvent>>(File.ReadAllText(rolling)) is { } r)
                    events.AddRange(r);
            }
            catch { }
            return events.Count > 0 ? events : null;
        }
    }

    /// <summary>Append <paramref name="newHistory"/>; overwrite the rolling window when it's non-null.</summary>
    public void Save(string characterName, IReadOnlyList<TelemetryEvent> newHistory, IReadOnlyList<TelemetryEvent>? rolling)
    {
        lock (_lock)
        {
            var (historyPath, rollingPath, _) = PathsFor(characterName);
            try
            {
                if (newHistory.Count > 0)
                    File.AppendAllLines(historyPath, newHistory.Select(e => JsonSerializer.Serialize(e, LineOpts)));
            }
            catch { }
            try { if (rolling != null) WriteAtomic(rollingPath, JsonSerializer.Serialize(rolling)); }
            catch { }
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    private static void MigrateLegacy(string history, string rolling, string legacy)
    {
        if (!File.Exists(legacy) || File.Exists(history)) return;
        try
        {
            var events = JsonSerializer.Deserialize<List<TelemetryEvent>>(File.ReadAllText(legacy)) ?? new();
            File.WriteAllLines(history, events.Where(e => !BotTelemetry.IsRolling(e.Type)).Select(e => JsonSerializer.Serialize(e, LineOpts)));
            WriteAtomic(rolling, JsonSerializer.Serialize(events.Where(e => BotTelemetry.IsRolling(e.Type)).TakeLast(BotTelemetry.MaxRolling).ToList()));
            File.Move(legacy, legacy + ".migrated", overwrite: true);
        }
        catch { }
    }

    private (string history, string rolling, string legacy) PathsFor(string characterName)
    {
        // Sanitize a character name into a safe filename. Mirrors BuildStore's approach.
        var safe = new string((characterName ?? "").Where(c => char.IsLetterOrDigit(c) || " _-[]()".Contains(c)).ToArray()).Trim();
        if (string.IsNullOrEmpty(safe)) safe = "unnamed";
        var b = Path.Combine(_dir, safe);
        return (b + ".jsonl", b + ".rolling.json", b + ".json");
    }
}
