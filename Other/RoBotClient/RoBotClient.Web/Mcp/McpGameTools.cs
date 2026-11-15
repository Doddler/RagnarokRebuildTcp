using System.ComponentModel;
using ModelContextProtocol.Server;
using RebuildSharedData.ClientTypes;
using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.Manager;

namespace RoBotClient.Web.Mcp;

/// <summary>MCP tools for reading the game database and forecasting fights with the battle simulator.</summary>
[McpServerToolType]
public static class McpGameTools
{
    [McpServerTool(Name = "plan_route"),
     Description("Plan a cross-map route via the region-aware world graph. Returns the ordered list of portal edges (source cell on the bot's current map, destination cell on the next map) the bot would take to get from (fromMap, fromX, fromY) to (toMap, toX, toY). Each edge knows both endpoints' cells AND the walkable-region indices on each map — so a returned plan is sub-region-trap-free by construction. Returns { built: false } when the graph isn't loaded (no warp scripts on disk).")]
    public static object PlanRoute(GameDatabase db,
        string fromMap, int fromX, int fromY, string toMap, int toX, int toY)
    {
        if (!db.World.IsBuilt)
            return new { built = false, message = "World graph not built — warp scripts not found at boot." };
        var plan = db.World.Plan(fromMap, fromX, fromY, toMap, toX, toY);
        if (plan == null) return new { built = true, ok = false, message = "No route in the graph." };
        return new
        {
            built = true,
            ok = true,
            cost = plan.Cost,
            edges = plan.Edges.Select((e, i) => new
            {
                step = i,
                srcMap = e.SrcMap, srcRegion = e.SrcRegion, srcX = e.Sx, srcY = e.Sy,
                dstMap = e.DstMap, dstRegion = e.DstRegion, dstX = e.Dx, dstY = e.Dy,
            }).ToList(),
        };
    }

    [McpServerTool(Name = "world_graph_status"),
     Description("Quick health-check on the region-aware world graph: whether it's built, how many region nodes and portal edges, and the regions on a specific map. Useful when diagnosing pathfinding bugs.")]
    public static object WorldGraphStatus(GameDatabase db, string? sampleMap = null)
    {
        if (!db.World.IsBuilt) return new { built = false };
        var sample = sampleMap != null && db.World.EdgesBySrcMap.TryGetValue(sampleMap, out var edges)
            ? edges.Select(e => new { srcRegion = e.SrcRegion, srcX = e.Sx, srcY = e.Sy,
                                       dstMap = e.DstMap, dstRegion = e.DstRegion,
                                       dstX = e.Dx, dstY = e.Dy }).ToList()
            : null;
        return new
        {
            built = true,
            nodes = db.World.NodeCount,
            edges = db.World.EdgeCount,
            droppedUnknownMaps = db.World.LastBuildDroppedUnknownMaps,
            kafraEdges = db.World.LastBuildKafraEdges,
            kafraNpcCount = db.Kafras.Count,
            sample,
        };
    }

    [McpServerTool(Name = "get_monster"),
     Description("Look up a monster by numeric id or code (e.g. 'PORING'). Returns full stats incl. AI/aggression, ScanDist (aggro radius), and drops.")]
    public static object GetMonster(GameDatabase db, string monster)
    {
        var m = ResolveMonster(db, monster);
        if (m == null) return new { error = $"No monster '{monster}'." };
        return new
        {
            m.Id, m.Code, m.Name, m.Level, m.HP, m.Exp, m.JExp,
            atkMin = m.AtkMin, atkMax = m.AtkMax, m.Def, m.MDef,
            m.Str, m.Agi, m.Vit, m.Int, m.Dex, m.Luk,
            m.Range, m.ScanDist, m.MoveSpeed, m.Size, m.Element, m.Race,
            ai = m.Ai, special = m.Special, aggressive = IsAggressive(m),
            drops = m.Drops?.Select(d => new { d.ItemId, name = db.ItemName(d.ItemId), d.Chance, d.CountMin, d.CountMax }).ToList(),
            spawns = m.Spawns?.Select(sp => new { sp.Map, sp.Count }).ToList(),
        };
    }

    [McpServerTool(Name = "find_monsters"),
     Description("Search monsters by level range and/or name substring. aggressiveOnly = only monsters that aggro on sight. Returns up to 'limit' brief entries sorted by level.")]
    public static object FindMonsters(GameDatabase db,
        int minLevel = 0, int maxLevel = 999, string? name = null, bool aggressiveOnly = false, int limit = 40)
    {
        var q = db.MonstersById.Values.Where(m => m.Level >= minLevel && m.Level <= maxLevel);
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(m => m.Name != null && m.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (aggressiveOnly)
            q = q.Where(IsAggressive);
        return q.OrderBy(m => m.Level).Take(Math.Clamp(limit, 1, 200))
            .Select(m => new
            {
                m.Id, m.Code, m.Name, m.Level, m.HP, atk = $"{m.AtkMin}-{m.AtkMax}", m.Def, m.Range, m.ScanDist,
                ai = m.Ai, special = m.Special, aggressive = IsAggressive(m),
            })
            .ToList();
    }

    [McpServerTool(Name = "get_item"),
     Description("Look up an item by numeric id, code, or exact name. Returns class, weight, buy/sell prices, slots, and equip position.")]
    public static object GetItem(GameDatabase db, string item)
    {
        var it = ResolveItem(db, item);
        if (it == null) return new { error = $"No item '{item}'." };
        return new
        {
            it.Id, it.Code, it.Name, it.Weight, it.Price, it.SellPrice, it.Slots, it.ItemRank,
            itemClass = it.ItemClass.ToString(), useType = it.UseType.ToString(), position = it.Position.ToString(),
            it.IsUnique, it.IsRefinable,
        };
    }

    [McpServerTool(Name = "find_traders"),
     Description("List NPC traders that sell a given item id, with their map and coordinates (use this to find where to buy potions/gear).")]
    public static object FindTraders(GameDatabase db, int itemId) =>
        db.TradersSelling(itemId).Select(n => new { n.Id, n.Name, n.Map, n.X, n.Y }).ToList();

    [McpServerTool(Name = "simulate_fight"),
     Description("Forecast a 1v1 fight between a bot (its current live stats) and a monster (id or code) using the server's damage math. Returns CanWin plus damage-per-hit, hit chances and seconds-to-kill each way. Routes through the bot's OWN Forecast pipeline, so every per-bot modifier applies: NoAutoAttack zeros the melee swing, SkillScript drives effectiveDpsWithSkills (FireBolt / DoubleStrafe / etc. at the rule's level), ItemScript potion rules add an HP buffer, immobile-mob free-win rule (Mandragora R5 plant / Geographer AiAggressiveImmobile) zeros incoming damage when the bot's attack range exceeds the mob's.")]
    public static object SimulateFight(BotManager bots, GameDatabase db, string botId, string monster)
    {
        var behavior = bots.GetBehavior(botId);
        if (behavior == null) return new { error = $"No bot '{botId}'." };
        var mdb = ResolveMonster(db, monster);
        if (mdb == null) return new { error = $"No monster '{monster}'." };

        var monImmobile = mdb.MoveSpeed <= 0
            || (!string.IsNullOrEmpty(mdb.Ai) && mdb.Ai.IndexOf("Immobile", StringComparison.OrdinalIgnoreCase) >= 0);
        // Delegate to the bot's Forecast — single source of truth so MCP results match what TickAsync's
        // PickTarget actually decides. Avoids the past bug where this MCP duplicated the logic minus
        // NoAutoAttack / SkillScript / potion-buffer / scripted DPS.
        var forecast = behavior.ForecastAgainst(mdb.Id);
        var diag = behavior.BuildSimDiagnostics();
        var freeWin = diag.NoAutoAttack
                      ? "skill-only — melee zeroed"
                      : (monImmobile && diag.AttackRange > mdb.Range
                          ? "immobile-out-of-range free kill"
                          : "standard");

        return new { monster = mdb.Name, monsterLevel = mdb.Level, monsterHp = mdb.HP,
                     monsterElement = mdb.Element, monsterSize = mdb.Size, monsterRace = mdb.Race,
                     monsterSpecial = mdb.Special, monsterRange = mdb.Range,
                     monsterImmobile = monImmobile,
                     aggressive = IsAggressive(mdb), forecast,
                     // Diagnostics — what fed the forecast on the bot side. Use these to verify the sim
                     // is honouring NoAutoAttack, SkillScript, and ItemScript correctly.
                     behaviorContext = new
                     {
                         noAutoAttack = diag.NoAutoAttack,
                         attackRange = diag.AttackRange,
                         magicAtkMin = diag.MagicAtkMin,
                         magicAtkMax = diag.MagicAtkMax,
                         potionHpBuffer = diag.PotionHpBuffer,
                         offensiveScriptedSkills = diag.OffensiveScriptedSkills
                             .Select(s => new { skill = s.Skill, level = s.Level, target = s.Target }).ToList(),
                         hpRescueItems = diag.HpRescueItems
                             .Select(r => new { itemId = r.ItemId, name = db.ItemName(r.ItemId),
                                                inventoryCount = r.InventoryCount, approxHealPerUse = r.ApproxHealPerUse }).ToList(),
                         engagementMode = freeWin,
                     } };
    }

    // Boss detection. Server's `MonsterDbEntry.Special` is a string set per data row — known values
    // include "Boss" / "MVP". Anything non-empty/non-"None" reads as "this is a special enemy".
    private static bool IsBossMonster(MonsterDbEntry mdb)
    {
        if (string.IsNullOrEmpty(mdb.Special)) return false;
        return mdb.Special.Equals("Boss", StringComparison.OrdinalIgnoreCase)
            || mdb.Special.Equals("MVP", StringComparison.OrdinalIgnoreCase);
    }

    [McpServerTool(Name = "simulate_group_fight"),
     Description("Forecast a bot fighting a whole pack at once (assist mobs / aggressive swarms): it kills them one at a time while every still-alive monster keeps hitting it. 'monsters' is a comma-separated list of monster ids/codes; 'count' repeats the whole list that many times (e.g. monster=PORING count=3 = three Porings). Returns CanWin plus total seconds-to-kill and total damage taken vs the bot's HP.")]
    public static object SimulateGroupFight(BotManager bots, GameDatabase db, string botId, string monsters, int count = 1)
    {
        var session = bots.GetSession(botId);
        if (session == null) return new { error = $"No bot '{botId}'." };

        var entries = new List<MonsterDbEntry>();
        foreach (var key in monsters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var mdb = ResolveMonster(db, key);
            if (mdb == null) return new { error = $"No monster '{key}'." };
            entries.Add(mdb);
        }
        if (entries.Count == 0) return new { error = "No monsters specified." };
        count = Math.Clamp(count, 1, 20);

        var pack = new List<MonsterDbEntry>(entries.Count * count);
        for (var i = 0; i < count; i++) pack.AddRange(entries);

        var result = session.WithState(w =>
        {
            var s = w.Self;
            var hasSelf = w.Entities.TryGetValue(s.EntityId, out var e);
            var hp = hasSelf ? e!.Hp : s.Hp;
            var maxHp = hasSelf ? e!.MaxHp : s.MaxHp;
            var me = new Combatant(s.Level, s.Attack, Math.Max(s.Attack, s.AttackMax), s.Def, s.Vit, s.Dex, s.Agi, s.Hit, s.Flee, s.AttackSpeed, hp, maxHp);
            var mons = new List<Combatant>(pack.Count);
            var mods = new List<CombatModifiers>(pack.Count);
            foreach (var mdb in pack)
            {
                mons.Add(new Combatant(mdb.Level, mdb.AtkMin, mdb.AtkMax, mdb.Def, mdb.Vit, mdb.Dex, mdb.Agi, 0, 0, 0, mdb.HP, mdb.HP));
                var (defEle, defLvl) = ElementChart.Parse(mdb.Element ?? "");
                mods.Add(new CombatModifiers(
                    DefenderElement: defEle, DefenderElementLevel: defLvl,
                    DefenderSize: WeaponSizeChart.ParseSize(mdb.Size ?? ""),
                    DefenderRace: WeaponSizeChart.ParseRace(mdb.Race ?? "")));
            }
            var playerMods = new CombatModifiers(
                AttackerElement: SimElement.Neutral,
                AttackerWeapon: SimWeaponClass.OneHandSword,
                AttackerLuk: s.Luk,
                AttackerAddCrit: s.AddCrit);
            return BattleSimulator.ForecastGroup(me, mons, mods, playerMods, 0.6, 30);
        });

        return new
        {
            pack = pack.Select(m => m.Name).ToList(),
            packSize = pack.Count,
            result.CanWin, result.TotalSecondsToKill, result.TotalDamageTaken, result.MyHp,
            result.WinChancePercent, result.AverageElementMod, result.AverageSizeMod, result.EffectiveDpsWithSkills,
        };
    }

    internal static MonsterDbEntry? ResolveMonster(GameDatabase db, string key)
    {
        if (int.TryParse(key, out var id)) return db.Monster(id);
        return db.ClassIdOf(key) is int cid ? db.Monster(cid) : null;
    }

    internal static ItemData? ResolveItem(GameDatabase db, string key)
    {
        if (int.TryParse(key, out var id)) return db.Item(id);
        return db.ItemsById.Values.FirstOrDefault(i =>
            string.Equals(i.Name, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Code, key, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAggressive(MonsterDbEntry m)
    {
        if (string.Equals(m.Special, "Boss", StringComparison.OrdinalIgnoreCase)) return true;
        var ai = m.Ai ?? "";
        return ai.StartsWith("AiAggressive", StringComparison.Ordinal) || ai == "AiAngry" || ai == "AiStandardBoss";
    }
}
