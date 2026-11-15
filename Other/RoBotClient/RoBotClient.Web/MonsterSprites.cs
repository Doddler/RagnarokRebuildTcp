using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace RoBotClient.Web;

/// <summary>
/// First frame of a monster's sprite, for small portraits. The Unity sprite importer packs each sprite's frames into
/// Sprites/Imported/Monsters/Atlas/&lt;sprite&gt;_atlas.png and records every frame's rect in &lt;sprite&gt;.asset;
/// monsterclass.json maps monster names to sprite files. Resolved on first use and cached.
/// </summary>
public sealed class MonsterSprites
{
    /// <summary>Frame rect inside the atlas, in pixels from the atlas's top-left corner.</summary>
    public sealed record Frame(string AtlasName, int X, int Top, int Width, int Height);

    private readonly string _monstersDir;
    private readonly Lazy<Dictionary<string, string>> _spriteByName;
    private readonly ConcurrentDictionary<string, Frame?> _frames = new(StringComparer.OrdinalIgnoreCase);

    public MonsterSprites(string clientConfigDir, string importedMonstersDir)
    {
        _monstersDir = importedMonstersDir;
        _spriteByName = new(() => LoadSpriteNames(Path.Combine(clientConfigDir, "monsterclass.json")));
    }

    public Frame? Get(string monsterName) =>
        _monstersDir.Length == 0
            ? null
            : _frames.GetOrAdd(monsterName, n => _spriteByName.Value.TryGetValue(n, out var sprite) ? ReadFirstFrame(sprite) : null);

    private static Dictionary<string, string> LoadSpriteNames(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return map;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var m in doc.RootElement.GetProperty("Items").EnumerateArray())
        {
            if (m.TryGetProperty("Name", out var n) && n.GetString() is { Length: > 0 } name
                && m.TryGetProperty("SpriteName", out var s) && s.GetString() is { Length: > 0 } sprite)
                map.TryAdd(name, Path.GetFileNameWithoutExtension(sprite));
        }
        return map;
    }

    private Frame? ReadFirstFrame(string sprite)
    {
        var asset = Path.Combine(_monstersDir, sprite + ".asset");
        var atlas = Path.Combine(_monstersDir, "Atlas", sprite + "_atlas.png");
        if (!File.Exists(asset) || !File.Exists(atlas))
            return null;

        // Unity YAML: the frame's "m_Name: sprite_<sprite>_0000" line is followed by its m_Rect x/y/width/height.
        var target = $"m_Name: sprite_{sprite}_0000";
        var found = false;
        double? x = null, y = null, w = null, h = null;
        foreach (var raw in File.ReadLines(asset))
        {
            var line = raw.Trim();
            if (!found) { found = line == target; continue; }
            if (line.StartsWith("x: ")) x = Num(line[3..]);
            else if (line.StartsWith("y: ")) y = Num(line[3..]);
            else if (line.StartsWith("width: ")) w = Num(line[7..]);
            else if (line.StartsWith("height: ")) { h = Num(line[8..]); break; }
        }
        if (x is null || y is null || w is null || h is null)
            return null;

        // Unity rects start at the texture's bottom edge; CSS offsets start at the top. PNG height is bytes 20-23.
        Span<byte> header = stackalloc byte[24];
        using (var f = File.OpenRead(atlas))
            f.ReadExactly(header);
        var textureHeight = BinaryPrimitives.ReadInt32BigEndian(header[20..]);

        return new Frame(sprite + "_atlas", (int)x, textureHeight - (int)y - (int)h, (int)w, (int)h);

        static double Num(string s) => Math.Round(double.Parse(s, CultureInfo.InvariantCulture));
    }
}
