namespace RoBotClient.Web.Mcp;

/// <summary>
/// Per-bot build plans persisted as markdown files so an agent can write a plan and resume it in a
/// later session. Keyed by a (sanitized) name — typically the bot's character name.
/// </summary>
public sealed class BuildStore
{
    private readonly string _dir;

    public BuildStore(string dir)
    {
        _dir = string.IsNullOrWhiteSpace(dir) ? Path.Combine(AppContext.BaseDirectory, "BotBuilds") : dir;
        System.IO.Directory.CreateDirectory(_dir);
    }

    public string Read(string name)
    {
        var path = PathFor(name);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    public void Write(string name, string content) => File.WriteAllText(PathFor(name), content ?? "");

    public List<string> List() =>
        System.IO.Directory.Exists(_dir)
            ? System.IO.Directory.GetFiles(_dir, "*.md").Select(p => Path.GetFileNameWithoutExtension(p)!).OrderBy(s => s).ToList()
            : new List<string>();

    private string PathFor(string name)
    {
        var safe = new string((name ?? "").Where(c => char.IsLetterOrDigit(c) || " _-[]()".Contains(c)).ToArray()).Trim();
        return Path.Combine(_dir, (string.IsNullOrEmpty(safe) ? "unnamed" : safe) + ".md");
    }
}
