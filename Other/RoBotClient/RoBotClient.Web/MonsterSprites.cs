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
    private readonly ConcurrentDictionary<string, Anim?> _anims = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every action of a monster sprite, for the minimap animator. Rects: [x, top, w, h] per sprite index.
    /// Actions: motion * 8 + facing (idle 0, walk 8, attack 16, hit 24, dead 32), each with its frame delay (ms) and
    /// frames of layers [sprite index, x, y, mirror 0/1, scaleX, scaleY, alpha]; x/y place the layer's centre
    /// relative to the feet, y down (RO act coordinates, as Unity's SpriteMeshBuilder reads them).</summary>
    public sealed record Anim(string Atlas, List<int[]> Rects, List<AnimAction> Actions);
    public sealed record AnimAction(int Delay, List<List<double[]>> Frames);

    public Anim? GetAnim(string monsterName) =>
        _monstersDir.Length == 0
            ? null
            : _anims.GetOrAdd(monsterName, n => _spriteByName.Value.TryGetValue(n, out var sprite) ? ReadAnim(sprite) : null);

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

    // Hand-rolled walk over the Unity YAML (no YAML package here): Sprite documents give each fileID's m_Rect; the
    // RoSpriteData MonoBehaviour lists Actions -> Frames -> Layers, then Sprites (fileIDs in layer-index order).
    // "Pos:" entries also start with "- Position:" at the layer indent, so they're skipped by tracking inPos.
    private Anim? ReadAnim(string sprite)
    {
        var asset = Path.Combine(_monstersDir, sprite + ".asset");
        var atlas = Path.Combine(_monstersDir, "Atlas", sprite + "_atlas.png");
        if (!File.Exists(asset) || !File.Exists(atlas))
            return null;

        var rectById = new Dictionary<string, double[]>();
        var spriteIds = new List<string>();
        var actions = new List<AnimAction>();
        List<double[]>? frame = null;
        double[]? layer = null;
        string? docId = null;
        double[] rect = new double[4];
        bool inPos = false, inSprites = false;
        foreach (var raw in File.ReadLines(asset))
        {
            if (raw.StartsWith("--- "))
            {
                docId = raw[(raw.IndexOf('&') + 1)..].Trim();
                rect = new double[4];
                inSprites = false;
                continue;
            }
            var line = raw.Trim();
            if (inSprites)
            {
                if (line.StartsWith("- {fileID: ")) { spriteIds.Add(line[11..^1]); continue; }
                inSprites = false;
            }
            if (line.StartsWith("x: ")) rect[0] = Num(line[3..]);
            else if (line.StartsWith("y: ")) rect[1] = Num(line[3..]);
            else if (line.StartsWith("width: ")) rect[2] = Num(line[7..]);
            else if (line.StartsWith("height: ")) { rect[3] = Num(line[8..]); rectById[docId ?? ""] = rect; }
            else if (line.StartsWith("- Delay: ")) actions.Add(new AnimAction((int)Num(line[9..]), new()));
            else if (line == "- Layers:" && actions.Count > 0) { actions[^1].Frames.Add(frame = new()); inPos = false; }
            else if (line.StartsWith("Pos:")) inPos = true;
            else if (line.StartsWith("- Position: ") && !inPos && frame != null)
            {
                var (px, py) = Vec(line[12..]);
                frame.Add(layer = [0, px, py, 0, 1, 1, 1]);
            }
            else if (layer != null && !inPos && line.StartsWith("Index: ")) layer[0] = Num(line[7..]);
            else if (layer != null && !inPos && line.StartsWith("IsMirror: ")) layer[3] = Num(line[10..]);
            else if (layer != null && !inPos && line.StartsWith("Scale: ")) (layer[4], layer[5]) = Vec(line[7..]);
            else if (layer != null && !inPos && line.StartsWith("Color: ")) layer[6] = Num(line[(line.LastIndexOf("a: ") + 3)..^1]);
            else if (line == "Sprites:") inSprites = true;
        }
        if (spriteIds.Count == 0 || actions.Count == 0)
            return null;

        Span<byte> header = stackalloc byte[24];
        using (var f = File.OpenRead(atlas))
            f.ReadExactly(header);
        var textureHeight = BinaryPrimitives.ReadInt32BigEndian(header[20..]);
        var rects = spriteIds.Select(id => rectById.TryGetValue(id, out var r)
            ? new[] { (int)r[0], textureHeight - (int)r[1] - (int)r[3], (int)r[2], (int)r[3] }
            : new[] { 0, 0, 0, 0 }).ToList();
        return new Anim($"monster-atlas/{Uri.EscapeDataString(sprite + "_atlas")}.png", rects, actions);

        static double Num(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        static (double, double) Vec(string s) // "{x: 1, y: -12}"
        {
            var p = s.Trim('{', '}').Split(',');
            return (Num(p[0].Split(':')[1]), Num(p[1].Split(':')[1]));
        }
    }
}
