using System.Text.RegularExpressions;
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

    /// <summary>Base exp needed to level up, from levelchart.txt: entry N (0-based) takes level N+1 to N+2.
    /// Empty when the file isn't exported.</summary>
    public IReadOnlyList<int> BaseExpToNextLevel { get; private set; } = Array.Empty<int>();

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

        var levelChart = Path.Combine(clientConfigDir, "levelchart.txt");
        if (File.Exists(levelChart))
            db.BaseExpToNextLevel = File.ReadAllLines(levelChart)
                .Where(l => l.Trim().Length > 0)
                .Select(l => int.Parse(l.Trim(), System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();

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
            // ponytail: gear stats aren't in the client export, so read the server CSVs that sit next to Script/Warps
            db.LoadGear(Path.GetFullPath(Path.Combine(warpScriptDir, "..", "..", "Db")));
        }

        return db;
    }

    // ---- gear (attack / defense / who can wear it) ----

    /// <summary>One wearable item. Type is the weapon class (Sword, Bow…), the armor type (Armor, Shield,
    /// Headgear…) or "Arrow"; Position is MainHand/BothHands for weapons, Top/Mid/Bottom… for headgear.</summary>
    public sealed record GearInfo(int Id, string Type, string Position, int Power, int MinLvl, string Group, int Range = 0);

    public IReadOnlyDictionary<int, GearInfo> Gear { get; private set; } = new Dictionary<int, GearInfo>();
    private Dictionary<string, string[]> _equipGroups = new(StringComparer.OrdinalIgnoreCase);

    private void LoadGear(string dbDir)
    {
        if (!Directory.Exists(dbDir)) return;
        // None of these files quote fields that contain commas, so a plain split is enough.
        IEnumerable<string[]> Rows(string file) => File.Exists(Path.Combine(dbDir, file))
            ? File.ReadLines(Path.Combine(dbDir, file)).Skip(1).Select(l => l.Split(',')).Where(c => c.Length > 3)
            : Enumerable.Empty<string[]>();
        static int I(string s) => int.TryParse(s, out var v) ? v : 0;

// Auto-attack cadence, as the server's Monster.UpdateStats: max(RechargeTime col 23, AttackTime col 25), in ms.
        // The client export leaves both out.
        foreach (var c in Rows("Monsters.csv"))
            {
            if (c.Length > 25 && Math.Max(I(c[23]), I(c[25])) is > 0 and var ms) _attackDelay[I(c[0])] = ms / 1000.0;
            if (c.Length > 6) _monsterInt[I(c[0])] = I(c[6]); // MATK comes from INT (Monster.InitializeStats)
            if (c.Length > 10) _monsterLuk[I(c[0])] = I(c[10]); // status-chance luck roll
        }

        var gear = new Dictionary<int, GearInfo>();
        // Id,Code,Name,Attack,Range,Slot,Price,Weight,Type,Position,Property,MinLvl,Rank,EquipGroup
        foreach (var c in Rows("ItemsWeapons.csv"))
            gear[I(c[0])] = new GearInfo(I(c[0]), c[8], c[9], I(c[3]), I(c[11]), c[13], I(c[4]));
        // Id,Code,Name,Defense,MagicDef,Slot,Price,Weight,Type,Position,Property,MinLvl,EquipGroup
        foreach (var c in Rows("ItemsEquipment.csv"))
            gear[I(c[0])] = new GearInfo(I(c[0]), c[8], c[9], I(c[3]), I(c[11]), c[12]);
        // Id,Code,Name,Attack,Price,Weight,Property,MinLvl,Type
        foreach (var c in Rows("ItemsAmmo.csv"))
            gear[I(c[0])] = new GearInfo(I(c[0]), c[8], "", I(c[3]), I(c[7]), "");
        Gear = gear;

        // Group,"Display name",member,member… where a member is a job name or an earlier group.
        var groups = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(Path.Combine(dbDir, "EquipmentGroups.csv")))
            foreach (var c in File.ReadLines(Path.Combine(dbDir, "EquipmentGroups.csv")).Select(l => l.Split(',')))
                if (c.Length > 2) groups[c[0].Trim()] = c[2..].Select(s => s.Trim()).ToArray();
        _equipGroups = groups;

        // JobLvl,Novice,FirstJob,SecondJob  /  Id,Class,ExpChart,MaxJobLevel,...
        _jobExpToNext = Rows("ExpJobChart.csv").Select(c => c[1..].Select(I).ToArray()).ToArray();
        foreach (var c in Rows("Jobs.csv")) _jobTier[I(c[0])] = I(c[2]);
        // Id,Code,Name,Weight,Price,...
        var idByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in Rows("ItemsUsable.csv")) { _usablePrice[I(c[0])] = I(c[4]); _usableWeight[I(c[0])] = I(c[3]); idByCode[c[1]] = I(c[0]); }
        // RecoveryItem("Apple", 16, 22, 0, 0) — HP min/max, SP min/max, from the server's item effect script.
        var effects = Path.Combine(dbDir, "..", "Script", "Items", "ItemEffects.txt");
        if (File.Exists(effects))
            foreach (Match m in Regex.Matches(File.ReadAllText(effects), @"RecoveryItem\(""([^""]+)"",\s*(\d+),\s*(\d+)"))
                if (idByCode.TryGetValue(m.Groups[1].Value, out var id) && I(m.Groups[3].Value) > 0)
                    _hpRecovery[id] = (I(m.Groups[2].Value) + I(m.Groups[3].Value)) / 2;
        // Item("Awakening_Potion") { OnValidate: return CharacterLevel >= 40 && JobId != 4; OnUse: AddStatusEffect(StatusIncreasedAttackSpeed, 30m, 15); }
        if (File.Exists(effects))
            foreach (Match m in Regex.Matches(File.ReadAllText(effects), @"Item\(""([^""]+)""\)\s*\{([^}]*)\}"))
            {
                var body = m.Groups[2].Value;
                var validate = Regex.Match(body, @"OnValidate:\s*return\s+([^;]*);").Groups[1].Value;
                if (validate == "false" || !idByCode.TryGetValue(m.Groups[1].Value, out var id)
                    || Regex.Match(body, @"AddStatusEffect\(Status(\w+),\s*(\d+)([ms]),\s*(\d+)") is not { Success: true } fx
                    || !Enum.TryParse<CharacterStatusEffect>(fx.Groups[1].Value, out var status)) continue;
                var minLevel = Regex.Match(validate, @"CharacterLevel\s*>=\s*(\d+)") is { Success: true } lv ? I(lv.Groups[1].Value) : 0;
                int[] Jobs(string op) => Regex.Matches(validate, @"JobId\s*" + op + @"\s*(\d+)").Select(j => I(j.Groups[1].Value)).ToArray();
                _buffs[id] = new BuffItem(id, status, I(fx.Groups[2].Value) * (fx.Groups[3].Value == "m" ? 60 : 1), I(fx.Groups[4].Value), minLevel, Jobs("=="), Jobs("!="));
            }

        // Monsters with a [WasRangedAttacked] reaction (Greatest General's Earth Spike) can't be shot for free from range.
        // TryCast(FireBolt, 3, 5%, 1.5s, 5s) lines in the fighting states feed the forecast's skill damage.
        var skillDir = Path.Combine(dbDir, "..", "Script", "MonsterSkills");
        if (Directory.Exists(skillDir))
            foreach (var file in Directory.EnumerateFiles(skillDir, "*.txt", SearchOption.AllDirectories))
            {
                var code = "";
                var state = "";
                foreach (var line in File.ReadLines(file))
                {
                    if (Regex.Match(line, @"SkillHandler\(""(\w+)""\)") is { Success: true } h) { code = h.Groups[1].Value; state = ""; }
                    else if (code != "" && line.Contains("[WasRangedAttacked]")) _punishesRanged.Add(code);
                    else if (Regex.Match(line, @"^\s*State(\w+):") is { Success: true } st) state = st.Groups[1].Value;
                    else if (state is "Attacking" or "Chase" or "Any" && code != "" && Regex.Match(line, @"TryCast\((\w+),\s*(\d+),\s*([\d.]+%?),\s*([\d.]+s?),\s*([\d.]+s?)[^)]*\)\s*(?:\[([^\]]*)\])?") is { Success: true } tc
                             && Enum.TryParse<CharacterSkill>(tc.Groups[1].Value, out var skill)
                             && CastWeight(tc.Groups[6].Value) is > 0 and var weight)
                    {
                        var cast = new MonsterCast(skill, I(tc.Groups[2].Value), ScriptNumber(tc.Groups[3].Value),
                            ScriptNumber(tc.Groups[4].Value) / 1000.0, ScriptNumber(tc.Groups[5].Value) / 1000.0, weight);
                        foreach (var into in state switch { "Attacking" => new[] { _casts }, "Chase" => new[] { _chaseCasts }, _ => new[] { _casts, _chaseCasts } })
                        {
                            if (!into.TryGetValue(code, out var list)) into[code] = list = new();
                            // StateAny and the state's own line share one cooldown: keep the likelier line.
                            var same = list.FindIndex(x => x.Skill == skill);
                            if (same < 0) list.Add(cast);
                            else if (cast.ChancePerMille > list[same].ChancePerMille) list[same] = cast;
                        }
                    }
                }
            }

        // CreateSpawn("ANT_EGG", 1, %(28, 266, 0, 0), ...) pins spawns to x±w, y±h. The client export drops the area.
        var spawnDir = Path.Combine(dbDir, "..", "Script", "Spawns");
        if (Directory.Exists(spawnDir))
            foreach (var file in Directory.EnumerateFiles(spawnDir, "*.txt", SearchOption.AllDirectories))
            {
                var map = "";
                foreach (var line in File.ReadLines(file))
                {
                    if (Regex.Match(line, @"MapConfig\(""(\w+)""\)") is { Success: true } mapMatch) { map = mapMatch.Groups[1].Value; continue; }
                    var spawn = Regex.Match(line, @"CreateSpawn\(""(\w+)"",\s*(\d+),\s*%\(\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+)\s*\)");
                    if (!spawn.Success || map == "" || line.Contains("Boss") || line.Contains("Mvp")) continue;
                    int G(int i) => I(spawn.Groups[i].Value);
                    if (!_spawnAreas.TryGetValue(map, out var list)) _spawnAreas[map] = list = new();
                    list.Add(new SpawnArea(spawn.Groups[1].Value, G(2), G(3) - G(5), G(4) - G(6), G(3) + G(5), G(4) + G(6)));
                }
            }
    }

    /// <summary>A spawn pinned to a rectangle (inclusive cells) instead of anywhere on the map.</summary>
    public sealed record SpawnArea(string MonsterCode, int Count, int MinX, int MinY, int MaxX, int MaxY);
    private readonly Dictionary<string, List<SpawnArea>> _spawnAreas = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _punishesRanged = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, double> _attackDelay = new();
    private readonly Dictionary<int, int> _monsterInt = new();
    private readonly Dictionary<int, int> _monsterLuk = new();
    private readonly Dictionary<string, List<MonsterCast>> _casts = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>One TryCast a monster rolls on each auto-attack (StateAttacking / StateAny); cast and cooldown in
    /// seconds. Weight is the share of the fight its condition holds: 1 when unconditional, 0.3 for [HpPercent &lt; 30].</summary>
    public sealed record MonsterCast(CharacterSkill Skill, int Level, int ChancePerMille, double CastSeconds, double DelaySeconds, double Weight = 1);

    // [HpPercent < 30]: the monster spends about the last 30% of the fight below 30% HP.
    // ponytail: every other trigger (RudeAttacked, MinionsDeadFor, WasRangedAttacked…) counts as never firing in a
    // plain fight; they're teleports, summons and the ranged punish PunishesRangedAttacks already covers.
    private static double CastWeight(string condition) =>
        condition == "" ? 1
        : Regex.Match(condition, @"^\s*HpPercent\s*<=?\s*(\d+)\s*$") is { Success: true } hp ? int.Parse(hp.Groups[1].Value) / 100.0
        : 0;
    public IReadOnlyList<MonsterCast> MonsterCasts(string monsterCode) => _casts.TryGetValue(monsterCode, out var l) ? l : Array.Empty<MonsterCast>();
    private readonly Dictionary<string, List<MonsterCast>> _chaseCasts = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>TryCasts from StateChase / StateAny: what it throws at someone running away (rolled about once a second).</summary>
    public IReadOnlyList<MonsterCast> MonsterChaseCasts(string monsterCode) => _chaseCasts.TryGetValue(monsterCode, out var l) ? l : Array.Empty<MonsterCast>();
    public int MonsterInt(int classId) => _monsterInt.GetValueOrDefault(classId);
    public int MonsterLuk(int classId) => _monsterLuk.GetValueOrDefault(classId);

    // The server script's number literals (ScriptTreeWalker): "5%" = 50 per mille, "1.5s" = 1500 ms, bare = as is.
    private static int ScriptNumber(string s) =>
        s.EndsWith('%') ? (int)(double.Parse(s[..^1], System.Globalization.CultureInfo.InvariantCulture) * 10)
        : s.EndsWith('s') ? (int)(double.Parse(s[..^1], System.Globalization.CultureInfo.InvariantCulture) * 1000)
        : (int)double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    /// <summary>Seconds between a monster's auto-attacks; 0 when unknown (the simulator falls back to its default).</summary>
    public double MonsterAttackDelay(int classId) => _attackDelay.GetValueOrDefault(classId);
    public bool PunishesRangedAttacks(string monsterCode) => _punishesRanged.Contains(monsterCode);
    public IReadOnlyList<SpawnArea> SpawnAreas(string map) => _spawnAreas.TryGetValue(map, out var list) ? list : Array.Empty<SpawnArea>();

    private readonly Dictionary<int, int> _hpRecovery = new();
    private readonly Dictionary<int, BuffItem> _buffs = new();

    /// <summary>A usable item whose effect is a status (Concentration Potion: IncreasedAttackSpeed 1800 s, power 10).
    /// Only these jobs may drink it when OnlyJobs is non-empty; never NotJobs; MinLevel from its OnValidate.</summary>
    public sealed record BuffItem(int Id, CharacterStatusEffect Status, int Seconds, int Power, int MinLevel, int[] OnlyJobs, int[] NotJobs)
    {
        public bool Usable(int level, int jobId) => level >= MinLevel && !NotJobs.Contains(jobId) && (OnlyJobs.Length == 0 || OnlyJobs.Contains(jobId));
    }
    public IEnumerable<BuffItem> BuffItems => _buffs.Values;
    public BuffItem? Buff(int itemId) => _buffs.GetValueOrDefault(itemId);

    public enum ConsumableKind { None, Healing, Buff, Misc }
    /// <summary>Healing: one of the bot's own healing items (the potions it buys). Buff: a status effect an NPC sells
    /// (Concentration Potion). Misc: any other heal or buff, the stuff monsters drop (herbs, Monster's Feed, Apples; some
    /// shops sell food too, but it's never worth buying). None: everything else (wings, boxes, quest items).</summary>
    public ConsumableKind Consumable(int itemId, ICollection<int> healingItemIds)
    {
        if (_buffs.ContainsKey(itemId)) return Item(itemId)?.SoldBy is { Count: > 0 } ? ConsumableKind.Buff : ConsumableKind.Misc;
        if (_hpRecovery.ContainsKey(itemId)) return healingItemIds.Contains(itemId) ? ConsumableKind.Healing : ConsumableKind.Misc;
        return ConsumableKind.None;
    }
    /// <summary>Average HP an item restores (Apple 19, Novice Potion 27, Meat 85), 0 when it doesn't heal HP.</summary>
    public int HpRecovery(int itemId) => _hpRecovery.GetValueOrDefault(itemId);

    private readonly Dictionary<int, int> _usablePrice = new();
    private readonly Dictionary<int, int> _usableWeight = new();
    /// <summary>Weight of one usable item in the server's tenths (Fly Wing = 50), 0 if unknown.</summary>
    public int UsableItemWeight(int itemId) => _usableWeight.GetValueOrDefault(itemId);
    /// <summary>NPC buy price of a usable item (Fly Wing 140z), 0 when unknown.</summary>
    public int UsableItemPrice(int itemId) => _usablePrice.GetValueOrDefault(itemId);

    /// <summary>Whether a job (by name, e.g. "Archer") may wear the item, following the server's
    /// EquipmentGroups.csv nesting. Ammo has no group and is always allowed.</summary>
    public bool CanEquip(GearInfo g, string jobName)
    {
        if (g.Group.Length == 0) return true;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(g.Group);
        while (stack.Count > 0)
        {
            var name = stack.Pop();
            if (string.Equals(name, jobName, StringComparison.OrdinalIgnoreCase)) return true;
            if (seen.Add(name) && _equipGroups.TryGetValue(name, out var members))
                foreach (var m in members) stack.Push(m);
        }
        return false;
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

    // Job exp to next job level by job tier (ExpJobChart.csv: Novice, FirstJob, SecondJob) and each job's tier (Jobs.csv).
    private int[][] _jobExpToNext = Array.Empty<int[]>();
    private readonly Dictionary<int, int> _jobTier = new();

    public int BaseExpToNext(int level) => level >= 1 && level - 1 < BaseExpToNextLevel.Count ? BaseExpToNextLevel[level - 1] : 0;

    public int JobExpToNext(int jobId, int jobLevel)
    {
        var tier = _jobTier.GetValueOrDefault(jobId);
        return jobLevel >= 1 && jobLevel - 1 < _jobExpToNext.Length && tier < _jobExpToNext[jobLevel - 1].Length
            ? _jobExpToNext[jobLevel - 1][tier] : 0;
    }

    /// <summary>Lifetime base exp: every completed level's requirement plus progress into the current level
    /// (the server's Experience stat resets to 0 on each level-up).</summary>
    public long TotalBaseExp(int level, int expIntoLevel)
    {
        long total = expIntoLevel;
        for (var l = 1; l < level && l - 1 < BaseExpToNextLevel.Count; l++)
            total += BaseExpToNextLevel[l - 1];
        return total;
    }

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
