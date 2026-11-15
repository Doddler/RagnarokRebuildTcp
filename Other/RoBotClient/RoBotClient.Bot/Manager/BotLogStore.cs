namespace RoBotClient.Bot.Manager;

/// <summary>
/// Per-character on-disk log mirror: every OnLog line from the session + behavior is tee'd to
/// <c>bot-logs/&lt;character&gt;.log</c> with a wall-clock timestamp. Lets a controlling agent (or me) tail
/// the file directly without going through MCP <c>get_bot.log</c>, which only returns the in-memory ring
/// buffer. Per-file lock so concurrent bots don't interleave bytes inside the same line.
/// </summary>
public sealed class BotLogStore
{
    private readonly string _dir;
    private readonly Dictionary<string, object> _locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _locksLock = new();

    public BotLogStore(string? dir = null)
    {
        _dir = string.IsNullOrWhiteSpace(dir) ? Path.Combine(AppContext.BaseDirectory, "bot-logs") : dir;
        try { Directory.CreateDirectory(_dir); } catch { }
    }

    public void Append(string characterName, string line)
    {
        if (string.IsNullOrWhiteSpace(characterName)) return;
        var path = PathFor(characterName);
        var stamped = $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}";
        var perFile = GetLock(characterName);
        try
        {
            lock (perFile) { File.AppendAllText(path, stamped); }
        }
        catch { /* disk errors swallowed — telemetry/ring buffer still capture the line */ }
    }

    /// <summary>Read the last <paramref name="lines"/> lines of a character's log (or all if smaller).
    /// Returns an empty array if no log exists yet. Used by the dashboard / MCP to expose persistent
    /// history beyond the in-memory ring buffer.</summary>
    public string[] Tail(string characterName, int lines = 200)
    {
        if (string.IsNullOrWhiteSpace(characterName) || lines <= 0) return Array.Empty<string>();
        var path = PathFor(characterName);
        if (!File.Exists(path)) return Array.Empty<string>();
        var perFile = GetLock(characterName);
        try
        {
            lock (perFile)
            {
                var all = File.ReadAllLines(path);
                if (all.Length <= lines) return all;
                var result = new string[lines];
                Array.Copy(all, all.Length - lines, result, 0, lines);
                return result;
            }
        }
        catch { return Array.Empty<string>(); }
    }

    private object GetLock(string name)
    {
        lock (_locksLock)
        {
            if (!_locks.TryGetValue(name, out var o)) _locks[name] = o = new object();
            return o;
        }
    }

    private string PathFor(string name)
    {
        var safe = new string(name.Where(c => char.IsLetterOrDigit(c) || " _-[]()".Contains(c)).ToArray()).Trim();
        if (string.IsNullOrEmpty(safe)) safe = "unnamed";
        return Path.Combine(_dir, safe + ".log");
    }
}
