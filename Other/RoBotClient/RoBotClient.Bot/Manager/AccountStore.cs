using System.Text.Json;

namespace RoBotClient.Bot.Manager;

/// <summary>One known account and the bot characters that have been logged into it.</summary>
public sealed class AccountRecord
{
    public string Account = "";
    public string Password = "";
    public List<string> Characters = new();
}

/// <summary>
/// Persistent registry of (account, password, character) tuples the bot client has successfully logged into.
/// Lets the UI and MCP "reconnect" to a previously-used character by name without the caller having to know
/// which account it lives on. Plain JSON next to the app — same trust model as the rest of the local-only
/// bot config (passwords are not secrets in this project).
/// </summary>
public sealed class AccountStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, AccountRecord> _byAccount = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        IncludeFields = true, // shared types use plain public fields
    };

    public AccountStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(AppContext.BaseDirectory, "accounts.json")
            : path;
        Load();
    }

    /// <summary>All known accounts (deep-copied), sorted by account name.</summary>
    public IReadOnlyList<AccountRecord> List()
    {
        lock (_lock)
        {
            return _byAccount.Values
                .OrderBy(a => a.Account, StringComparer.OrdinalIgnoreCase)
                .Select(Copy)
                .ToList();
        }
    }

    /// <summary>Find the account record that has <paramref name="characterName"/> on it. Matches either the
    /// "BotZero" base form or the "[BOT] BotZero" display form, case-insensitively.</summary>
    public AccountRecord? FindByCharacter(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName)) return null;
        var n1 = characterName;
        var n2 = characterName.StartsWith("[BOT] ", StringComparison.OrdinalIgnoreCase)
            ? characterName.Substring("[BOT] ".Length)
            : "[BOT] " + characterName;
        lock (_lock)
        {
            foreach (var rec in _byAccount.Values)
                foreach (var c in rec.Characters)
                    if (string.Equals(c, n1, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c, n2, StringComparison.OrdinalIgnoreCase))
                        return Copy(rec);
            return null;
        }
    }

    /// <summary>Record that <paramref name="account"/> has been logged into and that <paramref name="characterName"/>
    /// (the full "[BOT] X" form) exists on it. Idempotent — calling repeatedly updates the password and
    /// adds the character only if it isn't already listed.</summary>
    public void Register(string account, string password, string characterName)
    {
        if (string.IsNullOrWhiteSpace(account)) return;
        lock (_lock)
        {
            if (!_byAccount.TryGetValue(account, out var rec))
                _byAccount[account] = rec = new AccountRecord { Account = account, Password = password ?? "" };
            if (!string.IsNullOrEmpty(password)) rec.Password = password;
            if (!string.IsNullOrWhiteSpace(characterName) &&
                !rec.Characters.Any(c => string.Equals(c, characterName, StringComparison.OrdinalIgnoreCase)))
                rec.Characters.Add(characterName);
            Save();
        }
    }

    private static AccountRecord Copy(AccountRecord r) => new()
    {
        Account = r.Account,
        Password = r.Password,
        Characters = new List<string>(r.Characters),
    };

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            var list = JsonSerializer.Deserialize<List<AccountRecord>>(json, JsonOpts);
            if (list != null)
                _byAccount = list
                    .Where(r => !string.IsNullOrWhiteSpace(r.Account))
                    .ToDictionary(r => r.Account, r => r, StringComparer.OrdinalIgnoreCase);
        }
        catch { /* corrupted/missing file → start fresh */ }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var ordered = _byAccount.Values.OrderBy(a => a.Account, StringComparer.OrdinalIgnoreCase).ToList();
            File.WriteAllText(_path, JsonSerializer.Serialize(ordered, JsonOpts));
        }
        catch { /* disk errors swallowed — next change will retry */ }
    }
}
