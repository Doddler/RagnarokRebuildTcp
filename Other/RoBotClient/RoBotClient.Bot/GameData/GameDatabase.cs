using System.Text.Json;
using System.Text.Json.Serialization;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;

namespace RoBotClient.Bot.GameData;

/// <summary>
/// Reads the JSON the server's DataToClientUtility exports into
/// RebuildClient/Assets/StreamingAssets/ClientConfigGenerated, plus the per-map .walk grids under
/// RebuildClient/Assets/Maps/exportdata. Lets the bot resolve monsters, items, maps, the warp graph,
/// skill trees, and shop NPCs. The bot only reads these files.
/// </summary>
public sealed class GameDatabase
{
    public IReadOnlyDictionary<int, MonsterDbEntry> MonstersById { get; private set; } =
        new Dictionary<int, MonsterDbEntry>();
    public IReadOnlyDictionary<string, MonsterDbEntry> MonstersByCode { get; private set; } =
        new Dictionary<string, MonsterDbEntry>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, ClientMapEntry> MapsByCode { get; private set; } =
        new Dictionary<string, ClientMapEntry>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, MapWarpEntry> WarpsByMap { get; private set; } =
        new Dictionary<string, MapWarpEntry>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<int, ItemData> ItemsById { get; private set; } =
        new Dictionary<int, ItemData>();
    public IReadOnlyDictionary<int, ClientSkillTree> SkillTreesByClass { get; private set; } =
        new Dictionary<int, ClientSkillTree>();
    public IReadOnlyDictionary<CharacterSkill, SkillData> SkillInfoById { get; private set; } =
        new Dictionary<CharacterSkill, SkillData>();
    public IReadOnlyList<NpcEntry> Npcs { get; private set; } = Array.Empty<NpcEntry>();
    public IReadOnlyList<KafraEntry> Kafras { get; private set; } = Array.Empty<KafraEntry>();

    /// <summary>Region-aware cross-map pathfinder. Built from the warp .txt scripts + walkmaps at boot.
    /// Empty / unbuilt when warp scripts aren't on disk (e.g. dev environments without the server repo);
    /// callers should null-check and fall back to the legacy BFS in that case.</summary>
    public WorldGraph World { get; private set; } = new();

    private string _exportDataDir = "";
    private readonly Dictionary<string, WalkMap?> _walkCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _walkLock = new();

    private sealed class Envelope<T> { public List<T> Items { get; set; } = new(); }

    // The shared ClientTypes use public fields; field inclusion is mandatory. The string-enum converter
    // also reads numeric enum values, so it's safe whether a file serializes enums as names or numbers.
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static GameDatabase Load(string clientConfigDir, string exportDataDir = "", string? warpScriptDir = null)
    {
        var db = new GameDatabase { _exportDataDir = exportDataDir };

        var monsters = ReadEnvelope<MonsterDbEntry>(Path.Combine(clientConfigDir, "monsterdatabase.json"));
        var byId = new Dictionary<int, MonsterDbEntry>();
        var byCode = new Dictionary<string, MonsterDbEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in monsters)
        {
            byId[m.Id] = m;
            if (!string.IsNullOrEmpty(m.Code)) byCode[m.Code] = m;
        }
        db.MonstersById = byId;
        db.MonstersByCode = byCode;

        var maps = ReadEnvelope<ClientMapEntry>(Path.Combine(clientConfigDir, "maps.json"));
        var mapsByCode = new Dictionary<string, ClientMapEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in maps)
            mapsByCode[m.Code] = m;
        db.MapsByCode = mapsByCode;

        var warps = ReadEnvelope<MapWarpEntry>(Path.Combine(clientConfigDir, "mapwarps.json"));
        var warpsByMap = new Dictionary<string, MapWarpEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in warps)
            warpsByMap[w.Map] = w;
        db.WarpsByMap = warpsByMap;

        var items = ReadEnvelope<ItemData>(Path.Combine(clientConfigDir, "items.json"));
        var itemsById = new Dictionary<int, ItemData>();
        foreach (var it in items)
            itemsById[it.Id] = it;
        db.ItemsById = itemsById;

        var trees = ReadEnvelope<ClientSkillTree>(Path.Combine(clientConfigDir, "skilltree.json"));
        var treesByClass = new Dictionary<int, ClientSkillTree>();
        foreach (var t in trees)
            treesByClass[t.ClassId] = t;
        db.SkillTreesByClass = treesByClass;

        var skills = ReadEnvelope<SkillData>(Path.Combine(clientConfigDir, "skillinfo.json"));
        var skillsById = new Dictionary<CharacterSkill, SkillData>();
        foreach (var s in skills)
            skillsById[s.SkillId] = s;
        db.SkillInfoById = skillsById;

        db.Npcs = ReadEnvelope<NpcEntry>(Path.Combine(clientConfigDir, "npcdatabase.json"));
        db.Kafras = ReadEnvelope<KafraEntry>(Path.Combine(clientConfigDir, "kafradatabase.json"));

        // World graph (region-aware Dijkstra). Built from the warp .txt scripts which carry both
        // source AND destination cells — the existing mapwarps.json throws those away. The knownMaps
        // allowlist is the canonical map list from maps.json; warps referencing maps the server hasn't
        // registered (legacy WoE guild maps like pay_gld, etc.) are pruned so they can never appear in a
        // route. Kafra warps (free teleport NPCs) are added as additional edges so the planner can route
        // through them when they're faster than walking. Falls back silently when the script directory
        // isn't on disk (e.g. dev / dashboard-only deployments).
        if (!string.IsNullOrEmpty(warpScriptDir) && Directory.Exists(warpScriptDir))
        {
            var knownMaps = new HashSet<string>(db.MapsByCode.Keys, StringComparer.OrdinalIgnoreCase);
            db.World.Build(warpScriptDir, db.GetWalkMap, knownMaps, db.Kafras);
        }

        return db;
    }

    private static List<T> ReadEnvelope<T>(string path)
    {
        if (!File.Exists(path))
            return new List<T>();
        var env = JsonSerializer.Deserialize<Envelope<T>>(File.ReadAllText(path), Options);
        return env?.Items ?? new List<T>();
    }

    // ---- monsters / maps ----

    public MonsterDbEntry? Monster(int classId) =>
        MonstersById.TryGetValue(classId, out var m) ? m : null;

    public string MonsterName(int classId) =>
        MonstersById.TryGetValue(classId, out var m) ? m.Name : $"#{classId}";

    public string MapName(string code) =>
        MapsByCode.TryGetValue(code, out var m) ? m.Name : code;

    public int? ClassIdOf(string monsterCode) =>
        MonstersByCode.TryGetValue(monsterCode, out var m) ? m.Id : null;

    // ---- items ----

    public ItemData? Item(int itemId) =>
        ItemsById.TryGetValue(itemId, out var it) ? it : null;

    public string ItemName(int itemId) =>
        ItemsById.TryGetValue(itemId, out var it) ? it.Name : $"#{itemId}";

    /// <summary>Display name with the "[slots]" suffix for slotted gear and a "+refine" prefix when refined.</summary>
    public string ItemDisplayName(int itemId, byte refine = 0)
    {
        if (!ItemsById.TryGetValue(itemId, out var it))
            return $"#{itemId}";
        var name = it.Slots > 0 ? $"{it.Name} [{it.Slots}]" : it.Name;
        return refine > 0 ? $"+{refine} {name}" : name;
    }

    public bool IsUsableItem(int itemId) =>
        ItemsById.TryGetValue(itemId, out var it) && it.ItemClass == ItemClass.Useable && it.UseType != ItemUseType.NotUsable;

    // ---- skills ----

    public ClientSkillTree? SkillTree(int classId) =>
        SkillTreesByClass.TryGetValue(classId, out var t) ? t : null;

    public SkillData? SkillInfo(CharacterSkill skill) =>
        SkillInfoById.TryGetValue(skill, out var s) ? s : null;

    /// <summary>All skills a job can learn — its own tree plus every ancestor via ExtendsClass.</summary>
    public List<ClientSkillTreeEntry> LearnableSkills(int jobId)
    {
        var result = new List<ClientSkillTreeEntry>();
        var tree = SkillTree(jobId);
        var guard = 0;
        while (tree != null && guard++ < 16)
        {
            if (tree.Skills != null) result.AddRange(tree.Skills);
            if (tree.ExtendsClass < 0) break;
            tree = SkillTree(tree.ExtendsClass);
        }
        return result;
    }

    // ---- NPCs / shops ----

    public IEnumerable<NpcEntry> NpcsOnMap(string map) =>
        Npcs.Where(n => string.Equals(n.Map, map, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<NpcEntry> TradersSelling(int itemId) =>
        Npcs.Where(n => n.IsTrader && n.SellsItems != null && n.SellsItems.Contains(itemId));

    // ---- walkability ----

    /// <summary>Loads (and caches) a map's walkability grid, or null if the .walk file isn't found.</summary>
    public WalkMap? GetWalkMap(string mapCode)
    {
        if (string.IsNullOrEmpty(mapCode) || string.IsNullOrEmpty(_exportDataDir))
            return null;
        lock (_walkLock)
        {
            if (_walkCache.TryGetValue(mapCode, out var cached))
                return cached;
            var wm = WalkMap.LoadFromFile(Path.Combine(_exportDataDir, mapCode + ".walk"));
            _walkCache[mapCode] = wm;
            return wm;
        }
    }

    // ---- warp graph ----

    public IReadOnlyList<PortalEntry> PortalsOn(string map) =>
        WarpsByMap.TryGetValue(map, out var w) && w.Portals != null ? w.Portals : Array.Empty<PortalEntry>();

    private IEnumerable<string> Neighbors(string map) =>
        WarpsByMap.TryGetValue(map, out var w) && w.ConnectedTo != null ? w.ConnectedTo : Enumerable.Empty<string>();

    /// <summary>BFS over the warp graph: the neighbouring map to step into next on the way to <paramref name="to"/>.</summary>
    public string? NextHopToward(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return null;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { from };
        var queue = new Queue<(string map, string firstHop)>();
        foreach (var n in Neighbors(from))
            if (visited.Add(n)) queue.Enqueue((n, n));
        while (queue.Count > 0)
        {
            var (map, firstHop) = queue.Dequeue();
            if (string.Equals(map, to, StringComparison.OrdinalIgnoreCase)) return firstHop;
            foreach (var n in Neighbors(map))
                if (visited.Add(n)) queue.Enqueue((n, firstHop));
        }
        return null;
    }

    /// <summary>Warp-hop distance from <paramref name="from"/> to <paramref name="to"/> (0 same map, -1 unreachable).</summary>
    public int HopCount(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return 0;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { from };
        var queue = new Queue<(string map, int dist)>();
        foreach (var n in Neighbors(from))
            if (visited.Add(n)) queue.Enqueue((n, 1));
        while (queue.Count > 0)
        {
            var (map, dist) = queue.Dequeue();
            if (string.Equals(map, to, StringComparison.OrdinalIgnoreCase)) return dist;
            foreach (var n in Neighbors(map))
                if (visited.Add(n)) queue.Enqueue((n, dist + 1));
        }
        return -1;
    }

    /// <summary>Sum the danger scores of every intermediate map on the BFS shortest path from
    /// <paramref name="from"/> to <paramref name="to"/>. <paramref name="mapDanger"/> is a per-map score
    /// supplied by the caller (typically <c>BotBehavior.MapDanger</c>) — bot-specific because it depends
    /// on the bot's level + death history. Excludes the start map (we're already on it) and includes the
    /// destination's danger (we have to land there). Returns -1 if no path exists.</summary>
    public float ChainDanger(string from, string to, Func<string, float> mapDanger)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return 0f;
        // BFS that records the parent for each visited map, so we can reconstruct the path and sum the
        // danger of every map on it. Same shape as HopCount but stores predecessors.
        var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { from };
        var queue = new Queue<string>();
        foreach (var n in Neighbors(from))
            if (visited.Add(n)) { parent[n] = from; queue.Enqueue(n); }
        string? found = null;
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (string.Equals(cur, to, StringComparison.OrdinalIgnoreCase)) { found = cur; break; }
            foreach (var n in Neighbors(cur))
                if (visited.Add(n)) { parent[n] = cur; queue.Enqueue(n); }
        }
        if (found == null) return -1f;
        var sum = 0f;
        var node = found;
        while (node != null && !string.Equals(node, from, StringComparison.OrdinalIgnoreCase))
        {
            sum += mapDanger(node);
            node = parent.TryGetValue(node, out var par) ? par : null;
        }
        return sum;
    }

    public bool TryGetPortal(string from, string to, out int x, out int y)
    {
        foreach (var p in PortalsOn(from))
            if (string.Equals(p.To, to, StringComparison.OrdinalIgnoreCase)) { x = p.X; y = p.Y; return true; }
        x = 0; y = 0;
        return false;
    }

    public bool IsNearPortal(string map, int x, int y, int dist)
    {
        foreach (var p in PortalsOn(map))
            if (Math.Abs(p.X - x) <= dist && Math.Abs(p.Y - y) <= dist) return true;
        return false;
    }
}
