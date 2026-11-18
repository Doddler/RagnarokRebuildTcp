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

    /// <summary>A bot-logs file line in the buffer's "HH:mm:ss msg" form. The file stamps "yyyy-MM-dd HH:mm:ss.fff"
    /// (older files: "HH:mm:ss.fff").</summary>
    public static string BufferLine(string line) =>
        line.Length > 23 && line[4] == '-' && line[10] == ' ' && line[19] == '.' ? line[11..19] + line[23..]
        : line.Length > 12 && line[8] == '.' ? line[..8] + line[12..]
        : line;

    public void Append(string characterName, string line)
    {
        if (string.IsNullOrWhiteSpace(characterName)) return;
        var path = PathFor(characterName);
        // Dated: the files span days, and a time-only stamp made a Nov 15 portal loop read like a live bug on Nov 18.
        var stamped = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}";
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
