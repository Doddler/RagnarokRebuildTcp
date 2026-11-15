using System.Text.Json;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Manager;

/// <summary>
/// Persistent per-character telemetry. Each character gets one JSON file under <c>bot-telemetry/</c>
/// containing the full event list (capped at MaxEvents in <see cref="BotTelemetry"/>). Loaded once on
/// successful connect; saved periodically (only for bots whose <see cref="BotTelemetry.TakeDirty"/> says
/// there's something new to write).
/// </summary>
public sealed class BotTelemetryStore
{
    private readonly string _dir;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,                // event log can get big; skip pretty-print
        IncludeFields = false,                // TelemetryEvent is a record with positional properties
    };

    public BotTelemetryStore(string? dir = null)
    {
        _dir = string.IsNullOrWhiteSpace(dir)
            ? Path.Combine(AppContext.BaseDirectory, "bot-telemetry")
            : dir;
        try { Directory.CreateDirectory(_dir); } catch { }
    }

    public List<TelemetryEvent>? Load(string characterName)
    {
        var path = PathFor(characterName);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<TelemetryEvent>>(json, JsonOpts);
        }
        catch { return null; }
    }

    public void Save(string characterName, IReadOnlyList<TelemetryEvent> events)
    {
        try
        {
            var path = PathFor(characterName);
            File.WriteAllText(path, JsonSerializer.Serialize(events, JsonOpts));
        }
        catch { }
    }

    private string PathFor(string characterName)
    {
        // Sanitize a character name into a safe filename. Mirrors BuildStore's approach.
        var safe = new string((characterName ?? "").Where(c => char.IsLetterOrDigit(c) || " _-[]()".Contains(c)).ToArray()).Trim();
        if (string.IsNullOrEmpty(safe)) safe = "unnamed";
        return Path.Combine(_dir, safe + ".json");
    }
}
