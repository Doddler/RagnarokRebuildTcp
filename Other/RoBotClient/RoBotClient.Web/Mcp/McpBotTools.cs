using System.ComponentModel;
using ModelContextProtocol.Server;
using RebuildSharedData.Enum;
using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.Manager;
using RoBotClient.Bot.Session;
using RoBotClient.Bot.State;

namespace RoBotClient.Web.Mcp;

/// <summary>MCP tools for inspecting and controlling the running bots (shares the live BotManager).</summary>
[McpServerToolType]
public static class McpBotTools
{
    [McpServerTool(Name = "list_bots"),
     Description("List all running bots (level/job, HP/SP, map+position, FSM mode, target, kills, zeny, weight) PLUS any spawn failures from the last 5 minutes. If you call spawn_bot and the new bot doesn't appear in the bots list, check recentSpawnFailures — the silent-failure case (server kick / handshake timeout / character collision) lands there with a reason string.")]
    public static object ListBots(BotManager bots)
    {
        var failures = bots.GetRecentSpawnFailures();
        return new
        {
            bots = bots.GetSnapshots(),
            recentSpawnFailures = failures.Select(f => new
            {
                id = f.Id,
                account = f.Account,
                characterName = f.CharacterName,
                reason = f.Reason,
                atUtc = f.AtUtc,
                ageSeconds = (DateTime.UtcNow - f.AtUtc).TotalSeconds,
            }).ToList(),
        };
    }

    [McpServerTool(Name = "get_bot"),
     Description("Full detail for one bot: base stats, derived combat stats, stat/skill points, job id, inventory (with bagId + equipped flags), known skills, recent server errors, and recent log lines. 'logLines' trims the log tail (0 = no log, default 50, max 500).")]
    public static object GetBot(BotManager bots, GameDatabase db,
        [Description("Bot id, e.g. 'bot1'.")] string botId,
        [Description("Tail of the bot's log to include (0 = none, default 50, capped at 500).")] int logLines = 50)
    {
        var snap = bots.GetSnapshot(botId);
        var session = bots.GetSession(botId);
        if (snap == null || session == null) return new { error = $"No bot '{botId}'." };

        var detail = session.WithState(w =>
        {
            var s = w.Self;
            var equipped = new HashSet<int>();
            foreach (var b in s.EquippedBagIds) if (b != 0) equipped.Add(b);
            // Status effects on self — pulled from EntityView.Statuses (ApplyStatusEffect / RemoveStatusEffect
            // packets keep this live). Returns each active status by name with seconds remaining so the agent
            // can see "Poison: 8.4s" / "Blessing: 142.0s" alongside HP/SP.
            var statuses = new List<object>();
            if (w.Entities.TryGetValue(s.EntityId, out var selfView))
            {
                var now = DateTime.UtcNow;
                foreach (var kv in selfView.Statuses)
                {
                    var secs = (kv.Value - now).TotalSeconds;
                    if (secs <= 0) continue; // expired
                    statuses.Add(new { status = kv.Key.ToString(), secondsRemaining = Math.Round(secs, 1) });
                }
            }
            return new
            {
                stats = new { s.Str, s.Agi, s.Vit, s.Int, s.Dex, s.Luk },
                combat = new { atkMin = s.Attack, atkMax = s.AttackMax, s.Def, s.Hit, s.Flee, s.AttackSpeed },
                s.StatPoints,
                s.SkillPoints,
                jobId = s.JobId,
                inventory = s.Inventory.Select(it => new
                {
                    it.BagId, it.ItemId, name = db.ItemName(it.ItemId), it.Count, it.Refine,
                    equipped = equipped.Contains(it.BagId),
                }).ToList(),
                skills = s.KnownSkills.Select(k => new { skill = k.Skill.ToString(), k.Level }).ToList(),
                statusEffects = statuses,
            };
        });

        logLines = Math.Clamp(logLines, 0, 500);
        var fullLog = bots.GetLog(botId);
        var log = logLines >= fullLog.Length ? fullLog : fullLog[^logLines..];
        return new { snapshot = snap, detail, config = ConfigView(bots.GetBehaviorConfig(botId)), recentErrors = session.RecentErrors(), log };
    }

    [McpServerTool(Name = "get_telemetry"),
     Description("Per-bot, level-stamped telemetry: total kills/deaths/looted/used/level-ups/skill-casts, kills & deaths by monster, items looted/sold/bought/used, skill casts by skill, and seconds spent per map. minLevel drops events recorded below that bot level (age out stale data). eventLimit > 0 also returns the most recent N raw events (timestamped) for fine-grained inspection; 0 = no event list. Telemetry is persisted across stop+respawn / dashboard restart, so the counts reflect the bot's full known history.")]
    public static object GetTelemetry(BotManager bots, string botId, int minLevel = 0, int eventLimit = 0)
    {
        var s = bots.GetSession(botId);
        if (s == null) return new { error = $"No bot '{botId}'." };
        var t = s.Telemetry;
        object? recent = null;
        if (eventLimit > 0)
        {
            var all = t.Recent(minLevel);
            var take = Math.Min(eventLimit, 500); // hard cap on payload size
            var start = Math.Max(0, all.Count - take);
            recent = all.GetRange(start, all.Count - start)
                .Select(e => new { time = e.Time, level = e.Level, type = e.Type.ToString(), e.Detail, e.Value })
                .ToList();
        }
        return new
        {
            kills = t.Count(TelemetryEventType.Kill, minLevel),
            deaths = t.Count(TelemetryEventType.Death, minLevel),
            looted = t.Count(TelemetryEventType.Loot, minLevel),
            used = t.Count(TelemetryEventType.UsedItem, minLevel),
            levelUps = t.Count(TelemetryEventType.LevelUp, minLevel),
            skillCasts = t.Count(TelemetryEventType.SkillCast, minLevel),
            totalDamageDealt = t.SumValue(TelemetryEventType.DamageDealt, minLevel),
            totalDamageReceived = t.SumValue(TelemetryEventType.DamageReceived, minLevel),
            // Server-initiated disconnects (excludes clean stop_bot). A surge here indicates the bot is
            // session-flapping — check the bot-logs for the "Disconnected by server" lines + session
            // duration, and the new "entry handshake timed out" lines from BotSession.
            disconnects = t.Count(TelemetryEventType.Disconnect, minLevel),
            netDamageRatio = SafeRatio(t.SumValue(TelemetryEventType.DamageDealt, minLevel),
                                       t.SumValue(TelemetryEventType.DamageReceived, minLevel)),
            killsByMonster = t.CountByDetail(TelemetryEventType.Kill, minLevel),
            deathsByMonster = t.CountByDetail(TelemetryEventType.Death, minLevel),
            lootedItems = t.CountByDetail(TelemetryEventType.Loot, minLevel),
            soldItems = t.CountByDetail(TelemetryEventType.Sold, minLevel),
            boughtItems = t.CountByDetail(TelemetryEventType.Bought, minLevel),
            usedItems = t.CountByDetail(TelemetryEventType.UsedItem, minLevel),
            skillCastsByName = t.CountByDetail(TelemetryEventType.SkillCast, minLevel),
            damageDealtByTarget = t.CountByDetail(TelemetryEventType.DamageDealt, minLevel),
            damageReceivedFromAttacker = t.CountByDetail(TelemetryEventType.DamageReceived, minLevel),
            secondsPerMap = t.TimeByMap().ToDictionary(k => k.Key, v => (int)v.Value.TotalSeconds),
            recentEvents = recent,
        };
    }

    // Damage-dealt / damage-received ratio. > 1 means the bot is winning trades; < 1 means it's taking more
    // than it gives. Bounded so a divide-by-zero from "haven't been hit yet" doesn't break the JSON shape.
    private static double SafeRatio(long dealt, long received)
    {
        if (received <= 0) return dealt > 0 ? 999.99 : 0;
        return Math.Round((double)dealt / received, 2);
    }

    [McpServerTool(Name = "spawn_bot"),
     Description("Spawn a new bot — OR reconnect to a previously-used character. If 'characterName' matches a record in the account store (see list_accounts), the bot logs into the stored account and selects that character; isMale/hairStyle/hairColor are then ignored because the character already exists. Otherwise a fresh account+character is created. Other params: hunt map code, comma-separated hunt/ignore monster CODES, desired first job (1 Swordsman, 2 Archer, 3 Mage, 4 Acolyte, 5 Thief, 6 Merchant; 0 = none), appearance for creation (isMale, hairStyle 0-19, hairColor 0-8).")]
    public static object SpawnBot(BotManager bots, GameDatabase db,
        string? characterName = null, string? homeMap = null,
        string? huntCodes = null, string? ignoreCodes = null, int desiredJob = 0,
        bool isMale = true, int hairStyle = 0, int hairColor = 0)
    {
        var cfg = new BotBehaviorConfig { HomeMap = string.IsNullOrWhiteSpace(homeMap) ? "prt_fild08" : homeMap, DesiredJobId = desiredJob };
        ResolveCodes(db, cfg.HuntClassIds, huntCodes);
        ResolveCodes(db, cfg.IgnoreClassIds, ignoreCodes);

        try
        {
            if (!string.IsNullOrWhiteSpace(characterName))
            {
                var existingId = bots.SpawnExistingBot(cfg, characterName, isMale, hairStyle, hairColor);
                if (existingId != null)
                    return new { id = existingId, message = $"Reconnected to '{characterName}' as {existingId}.", reconnected = true };
            }

            var id = bots.SpawnBot(cfg, characterName, isMale: isMale, hairStyle: hairStyle, hairColor: hairColor);
            return new { id, message = $"Spawned {id}.", reconnected = false };
        }
        catch (InvalidOperationException ex)
        {
            // Session cooldown — the previous runner for this account just exited and the server still
            // holds the old connection. Surface the wait as a structured error so the agent can backoff
            // instead of hammering the server (which is what caused Cael's silent-fail reconnect loop).
            return new { error = ex.Message, cooldown = true };
        }
    }

    [McpServerTool(Name = "list_accounts"),
     Description("List the accounts the bot client has successfully logged into, with the characters known on each. Pass any of those character names to spawn_bot and the bot will reconnect with the stored credentials instead of creating a fresh account. If empty, call discover_accounts to scan the server for existing bot accounts.")]
    public static object ListAccounts(BotManager bots)
    {
        var store = bots.Accounts;
        if (store == null) return new { error = "AccountStore not registered." };
        return store.List().Select(r => new { account = r.Account, characters = r.Characters }).ToList();
    }

    [McpServerTool(Name = "discover_accounts"),
     Description("Probe bot account names 'bot_01'..'bot_NN' on the server with the default password and populate the account store with any characters that already exist. Useful when the store is empty (fresh dashboard restart, but bots from previous sessions still live on the server). Each probe logs in, lists characters, and disconnects — no bots are left running. maxAccount caps the scan (default 20, max 99). Returns { totalAccounts, accountsResponding }.")]
    public static async Task<object> DiscoverAccounts(BotManager bots, int maxAccount = 20)
    {
        var found = await bots.DiscoverAccountsAsync(maxAccount);
        return new
        {
            totalAccounts = bots.Accounts?.List().Count ?? 0,
            accountsResponding = found,
        };
    }

    [McpServerTool(Name = "buy_item"),
     Description("Drive a one-shot shop trip to buy a specific item. 'item' is an id, code, or exact name (e.g. 'ARROW', '1750', 'Bow'). The bot finds the nearest reachable trader that stocks it, travels there, opens the shop dialog, and buys what it can afford. Overrides the auto-restock loop for the duration of this trip. Returns immediately after queueing the trip; observe progress via get_bot (Mode=Shopping, recentErrors).")]
    public static object BuyItem(BotManager bots, GameDatabase db, string botId, string item, int quantity = 1)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        var it = McpGameTools.ResolveItem(db, item);
        if (it == null) return new { error = $"No item '{item}'." };
        if (quantity <= 0) return new { error = "Quantity must be > 0." };
        if (!db.TradersSelling(it.Id).Any())
            return new { error = $"No NPC stocks '{it.Name}' — nothing to buy." };
        b.RequestBuyItems(new[] { (it.Id, quantity) });
        return new { itemId = it.Id, itemName = it.Name, quantity, message = $"{botId} will travel to buy {quantity}x {it.Name}." };
    }

    [McpServerTool(Name = "sell_item"),
     Description("Drive a one-shot shop trip to sell a specific inventory item. 'item' is an id, code, or exact name. 'quantity' is how many to sell (0 = sell every stack of this item). Equipped items are skipped automatically. The bot heads to the nearest reachable trader and sells what it has. Returns immediately after queueing; observe progress via get_bot.")]
    public static object SellItem(BotManager bots, GameDatabase db, string botId, string item, int quantity = 0)
    {
        var b = bots.GetBehavior(botId);
        var s = bots.GetSession(botId);
        if (b == null || s == null) return new { error = $"No bot '{botId}'." };
        var it = McpGameTools.ResolveItem(db, item);
        if (it == null) return new { error = $"No item '{item}'." };
        var (have, equippedCount) = s.WithState(w =>
        {
            var equipped = new HashSet<int>();
            foreach (var bagId in w.Self.EquippedBagIds) if (bagId != 0) equipped.Add(bagId);
            var totalHave = 0;
            var eq = 0;
            foreach (var inv in w.Self.Inventory)
                if (inv.ItemId == it.Id)
                {
                    if (equipped.Contains(inv.BagId)) eq++;
                    else totalHave += inv.Count;
                }
            return (totalHave, eq);
        });
        if (have <= 0) return new { error = $"{botId} has no sellable '{it.Name}' in inventory (equipped stacks ignored: {equippedCount})." };
        b.RequestSellItems(new[] { (it.Id, quantity) });
        var label = quantity <= 0 ? $"all {have}" : Math.Min(quantity, have).ToString();
        return new { itemId = it.Id, itemName = it.Name, sellQuantity = label, message = $"{botId} will travel to sell {label}x {it.Name}." };
    }

    [McpServerTool(Name = "drop_item"),
     Description("Drop one or more units of an inventory item on the ground at the bot's feet. 'item' is an id, code, or exact name. 'quantity' is how many to drop (default 1). The drop is queued and executed on the next tick (~250ms latency). Equipped stacks are skipped. Returns the resolved item id + the count actually queued (capped at the unequipped stack total).")]
    public static object DropItem(BotManager bots, GameDatabase db, string botId, string item, int quantity = 1)
    {
        var b = bots.GetBehavior(botId);
        var s = bots.GetSession(botId);
        if (b == null || s == null) return new { error = $"No bot '{botId}'." };
        var it = McpGameTools.ResolveItem(db, item);
        if (it == null) return new { error = $"No item '{item}'." };
        if (quantity <= 0) return new { error = "Quantity must be > 0." };
        var unequippedHave = s.WithState(w =>
        {
            var equipped = new HashSet<int>();
            foreach (var bagId in w.Self.EquippedBagIds) if (bagId != 0) equipped.Add(bagId);
            var n = 0;
            foreach (var inv in w.Self.Inventory)
                if (inv.ItemId == it.Id && !equipped.Contains(inv.BagId)) n += inv.Count;
            return n;
        });
        if (unequippedHave <= 0) return new { error = $"{botId} has no unequipped '{it.Name}' to drop." };
        var dropQty = Math.Min(quantity, unequippedHave);
        b.RequestDropItems(new[] { (it.Id, dropQty) });
        return new { itemId = it.Id, itemName = it.Name, dropQty, message = $"{botId} will drop {dropQty}x {it.Name} on the next tick." };
    }

    [McpServerTool(Name = "queue_sell"),
     Description("Add items to the bot's pending sell queue — they'll be included in the NEXT natural auto-shop trip (when weight/stacks/junk-count triggers it), without disrupting the current activity. 'item' is an id, code, or exact name. 'quantity' = 0 means 'sell every stack of this item the next time we shop'. Use sell_item instead when you need an immediate shop trip.")]
    public static object QueueSell(BotManager bots, GameDatabase db, string botId, string item, int quantity = 0)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        var it = McpGameTools.ResolveItem(db, item);
        if (it == null) return new { error = $"No item '{item}'." };
        b.QueueSellItems(new[] { (it.Id, quantity) });
        var qtyLabel = quantity <= 0 ? "all" : quantity.ToString();
        return new { itemId = it.Id, itemName = it.Name, qty = qtyLabel,
            message = $"Queued {qtyLabel}x {it.Name} for the next natural auto-shop trip." };
    }

    [McpServerTool(Name = "get_sell_queue"),
     Description("Return the items currently queued for the bot's next natural auto-shop trip. Each entry is (itemId, itemName, qty) — qty 0 means 'all stacks'. Empty queue returns an empty list.")]
    public static object GetSellQueue(BotManager bots, GameDatabase db, string botId)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        var q = b.GetSellQueue();
        return new
        {
            count = q.Count,
            items = q.Select(e => new { itemId = e.itemId, itemName = db.ItemName(e.itemId), qty = e.qty }).ToList(),
        };
    }

    [McpServerTool(Name = "clear_sell_queue"),
     Description("Drop the bot's pending sell queue. The next auto-shop trip will only sell ShopPolicy junk again. No-op when the queue is already empty.")]
    public static object ClearSellQueue(BotManager bots, string botId)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        b.ClearSellQueue();
        return new { message = $"{botId}: sell queue cleared." };
    }

    [McpServerTool(Name = "set_restock"),
     Description("Add or update a target stock level for an item — every shop trip the bot takes, any visited NPC that stocks this item will top the bot up to this count (limited by zeny). 'item' = id/code/name; 'target' is the count to maintain. Use target=0 to remove the entry. Doesn't trigger a shop trip on its own — rides along whenever the bot naturally shops. Use force_restock to trigger an immediate trip.")]
    public static object SetRestock(BotManager bots, GameDatabase db, string botId, string item, int target)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        var it = McpGameTools.ResolveItem(db, item);
        if (it == null) return new { error = $"No item '{item}'." };
        b.SetRestockTarget(it.Id, target);
        return new
        {
            itemId = it.Id,
            itemName = it.Name,
            target,
            message = target <= 0
                ? $"Removed restock target for {it.Name}."
                : $"Set restock target {target}x {it.Name}.",
        };
    }

    [McpServerTool(Name = "get_restock_list"),
     Description("Return the bot's restock-target list: each item id, name, target count, and current held count. Useful to confirm what'll be topped up on the next shop trip.")]
    public static object GetRestockList(BotManager bots, GameDatabase db, string botId)
    {
        var b = bots.GetBehavior(botId);
        var s = bots.GetSession(botId);
        if (b == null || s == null) return new { error = $"No bot '{botId}'." };
        var targets = b.GetRestockTargets();
        var inv = s.WithState(w =>
        {
            var equipped = new HashSet<int>();
            foreach (var bid in w.Self.EquippedBagIds) if (bid != 0) equipped.Add(bid);
            var counts = new Dictionary<int, int>();
            foreach (var it in w.Self.Inventory)
                if (!equipped.Contains(it.BagId))
                    counts[it.ItemId] = counts.TryGetValue(it.ItemId, out var c) ? c + it.Count : it.Count;
            return counts;
        });
        return new
        {
            count = targets.Count,
            items = targets.Select(kv => new
            {
                itemId = kv.Key,
                itemName = db.ItemName(kv.Key),
                target = kv.Value,
                held = inv.TryGetValue(kv.Key, out var n) ? n : 0,
                shortfall = Math.Max(0, kv.Value - (inv.TryGetValue(kv.Key, out var n2) ? n2 : 0)),
            }).ToList(),
        };
    }

    [McpServerTool(Name = "clear_restock_list"),
     Description("Drop the entire restock-target list. The bot stops topping up these items on future shop trips. The single-item config RestockItemId (configure_bot.restockItemId) is unaffected.")]
    public static object ClearRestockList(BotManager bots, string botId)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        b.ClearRestockTargets();
        return new { message = $"{botId}: restock list cleared." };
    }

    [McpServerTool(Name = "set_no_auto_attack"),
     Description("Toggle the bot's auto-attack. When ON, the bot does NOT issue basic attacks — it depends entirely on its skill-script DSL to deal damage. Use for pure casters (Mage/Wizard) where staff auto-hits are wasted DPS, or for skill-only builds. Heals, walking, looting, party support all still work normally.")]
    public static object SetNoAutoAttack(BotManager bots, string botId, bool noAutoAttack)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return new { error = $"No bot '{botId}'." };
        cfg.NoAutoAttack = noAutoAttack;
        return new { botId, noAutoAttack, message = noAutoAttack
            ? $"{botId}: auto-attack OFF — skill script is the entire combat loop now."
            : $"{botId}: auto-attack ON — bot re-asserts AttackAsync every 3s when in range." };
    }

    [McpServerTool(Name = "force_hunt_add"),
     Description("Add a monster to the bot's force-hunt list. The bot will engage this monster CLASS even if its battle forecast says CanWin=false. Failsafe — operator overrides when the simulator is too cautious (e.g. before the ranged-fight model is wired for the bot's class). 'monster' = id or code or exact name. Returns the resolved class id.")]
    public static object ForceHuntAdd(BotManager bots, GameDatabase db, string botId, string monster)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return new { error = $"No bot '{botId}'." };
        var m = McpGameTools.ResolveMonster(db, monster);
        if (m == null) return new { error = $"No monster '{monster}'." };
        cfg.ForceHuntClassIds.Add(m.Id);
        return new { classId = m.Id, name = m.Name, count = cfg.ForceHuntClassIds.Count,
            message = $"{botId}: will now engage {m.Name} regardless of forecast verdict." };
    }

    [McpServerTool(Name = "force_hunt_remove"),
     Description("Remove a monster from the bot's force-hunt list. The bot returns to obeying the battle forecast for this class. No-op if the class wasn't in the list.")]
    public static object ForceHuntRemove(BotManager bots, GameDatabase db, string botId, string monster)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return new { error = $"No bot '{botId}'." };
        var m = McpGameTools.ResolveMonster(db, monster);
        if (m == null) return new { error = $"No monster '{monster}'." };
        cfg.ForceHuntClassIds.Remove(m.Id);
        return new { classId = m.Id, name = m.Name, count = cfg.ForceHuntClassIds.Count };
    }

    [McpServerTool(Name = "force_hunt_clear"),
     Description("Clear the bot's force-hunt list. Every target returns to obeying the forecast veto.")]
    public static object ForceHuntClear(BotManager bots, string botId)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return new { error = $"No bot '{botId}'." };
        cfg.ForceHuntClassIds.Clear();
        return new { message = $"{botId}: force-hunt list cleared." };
    }

    [McpServerTool(Name = "force_hunt_list"),
     Description("Return the bot's current force-hunt list — class ids that bypass the forecast veto.")]
    public static object ForceHuntList(BotManager bots, GameDatabase db, string botId)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return new { error = $"No bot '{botId}'." };
        var entries = cfg.ForceHuntClassIds
            .Select(id => new { classId = id, name = db.MonsterName(id) })
            .OrderBy(e => e.name).ToList();
        return new { count = entries.Count, items = entries };
    }

    [McpServerTool(Name = "force_restock"),
     Description("Force the bot to start a shop trip NOW to restock items from its restock list — useful when you want potions topped up before continuing without waiting for a natural sell trip. No-op when the list is empty or every item is already at/above target. Returns whether the trip was queued.")]
    public static object ForceRestock(BotManager bots, string botId)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        var queued = b.ForceRestockTrip();
        return new
        {
            queued,
            message = queued
                ? $"{botId}: shop trip queued to restock list items."
                : $"{botId}: nothing to restock (list empty or every item at target).",
        };
    }

    [McpServerTool(Name = "equip_card"),
     Description("Socket a card into a slotted piece of gear in the bot's inventory. 'card' = the card's item id/code/name (e.g. 'PORING_CARD'); 'gear' = the target weapon/armor item id/code/name (must have at least one free slot). Server validates the slot count, that the gear is the carded-slottable kind (UniqueItem, not crafted), and that the card's EquipPosition matches the gear's; on any mismatch an ErrorMessage lands in recentErrors.")]
    public static async Task<string> EquipCard(BotManager bots, GameDatabase db, string botId, string card, string gear)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        var cardIt = McpGameTools.ResolveItem(db, card);
        if (cardIt == null) return $"No item '{card}'.";
        var gearIt = McpGameTools.ResolveItem(db, gear);
        if (gearIt == null) return $"No item '{gear}'.";
        var (cardBagId, gearBagId) = s.WithState(w =>
        {
            var cbid = 0; var gbid = 0;
            foreach (var inv in w.Self.Inventory)
            {
                if (cbid == 0 && inv.ItemId == cardIt.Id && inv.Count > 0) cbid = inv.BagId;
                if (gbid == 0 && inv.ItemId == gearIt.Id && inv.Count > 0) gbid = inv.BagId;
            }
            return (cbid, gbid);
        });
        if (cardBagId == 0) return $"{botId} doesn't have '{cardIt.Name}' in inventory.";
        if (gearBagId == 0) return $"{botId} doesn't have '{gearIt.Name}' in inventory.";
        var before = DateTime.UtcNow;
        await s.SocketCardAsync(gearBagId, cardBagId);
        return await WithErrorReadback(s, before, $"Socketed '{cardIt.Name}' into '{gearIt.Name}' on {botId}.");
    }

    [McpServerTool(Name = "use_item"),
     Description("Use a consumable from the bot's inventory by id, code, or exact name (e.g. 'FLY_WING' — random same-map teleport; 'BUTTERFLY_WING' — return to save point; 'RED_POTION' — heal; etc.). Pre-checks: the item must be in inventory AND flagged Useable in the DB (an unknown/non-usable id would otherwise disconnect the player). target = -1 (default) = self; otherwise an entity id for items that target someone else.")]
    public static async Task<string> UseItem(BotManager bots, GameDatabase db, string botId, string item, int target = -1)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        var it = McpGameTools.ResolveItem(db, item);
        if (it == null) return $"No item '{item}'.";
        if (!db.IsUsableItem(it.Id)) return $"'{it.Name}' is not a usable consumable.";
        var have = s.WithState(w =>
        {
            foreach (var inv in w.Self.Inventory)
                if (inv.ItemId == it.Id && inv.Count > 0) return true;
            return false;
        });
        if (!have) return $"{botId} has no '{it.Name}' in inventory.";
        var before = DateTime.UtcNow;
        await s.UseInventoryItemAsync(it.Id, target);
        return await WithErrorReadback(s, before, $"Used '{it.Name}' on {botId}.");
    }

    [McpServerTool(Name = "equip_item_by_name"),
     Description("Equip an inventory item on a bot by id, code, or exact name (e.g. 'ARROW', 'KNIFE'). Finds the first matching stack in the bot's bag and sends the standard equip packet — works for ammunition (archers need arrows equipped before bow attacks/skills work), weapons, armor, accessories, etc. Server rejections (wrong job, level too low, invalid position) surface in recentErrors.")]
    public static async Task<string> EquipItemByName(BotManager bots, GameDatabase db, string botId, string item)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        var it = McpGameTools.ResolveItem(db, item);
        if (it == null) return $"No item '{item}'.";
        var bagId = s.WithState(w =>
        {
            foreach (var inv in w.Self.Inventory)
                if (inv.ItemId == it.Id && inv.Count > 0) return inv.BagId;
            return 0;
        });
        if (bagId == 0) return $"{botId} doesn't have '{it.Name}' in inventory.";
        var before = DateTime.UtcNow;
        await s.EquipItemAsync(bagId);
        return await WithErrorReadback(s, before, $"Equipping bag {bagId} ({it.Name}) on {botId}.");
    }

    [McpServerTool(Name = "stop_bot"), Description("Stop and remove a running bot.")]
    public static string StopBot(BotManager bots, string botId) =>
        bots.StopBot(botId) ? $"Stopped {botId}." : $"No bot '{botId}'.";

    [McpServerTool(Name = "configure_bot"),
     Description("Update a running bot's behavior config. Omitted fields are unchanged. Monster lists are comma-separated CODES; healingItemIds/lootBlacklist are comma-separated item ids. Fractional units: healHpPercent and restBelowPercent are 0..1 (e.g. 0.5 = 50%); winMargin is 0..2 (smaller = pickier targets). partyRole = Auto|Tank|Dps|Healer|Buffer|Utility (Auto derives from JobId at runtime).")]
    public static string ConfigureBot(BotManager bots, GameDatabase db, string botId,
        bool? enabled = null, string? homeMap = null,
        string? huntCodes = null, string? fleeCodes = null, string? ignoreCodes = null,
        bool? autoLoot = null, string? lootBlacklist = null,
        bool? autoShop = null, double? healHpPercent = null, string? healingItemIds = null,
        int? desiredJob = null, bool? verbose = null,
        double? winMargin = null, int? maxRoundsToKill = null, int? maxTargetHp = null,
        bool? restWhenNoPotions = null, double? restBelowPercent = null,
        bool? autoEscapeWhenStuck = null, string? partyRole = null, string? itemScript = null,
        bool? announceTarget = null, bool? listenToPartyChat = null)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return $"No bot '{botId}'.";
        if (enabled.HasValue) cfg.Enabled = enabled.Value;
        if (homeMap != null) cfg.HomeMap = homeMap;
        if (huntCodes != null) { cfg.HuntClassIds.Clear(); ResolveCodes(db, cfg.HuntClassIds, huntCodes); }
        if (fleeCodes != null) { cfg.FleeClassIds.Clear(); ResolveCodes(db, cfg.FleeClassIds, fleeCodes); }
        if (ignoreCodes != null) { cfg.IgnoreClassIds.Clear(); ResolveCodes(db, cfg.IgnoreClassIds, ignoreCodes); }
        if (autoLoot.HasValue) cfg.AutoLoot = autoLoot.Value;
        if (lootBlacklist != null) { cfg.LootBlacklist.Clear(); foreach (var n in ParseInts(lootBlacklist)) cfg.LootBlacklist.Add(n); }
        if (autoShop.HasValue) cfg.AutoShop = autoShop.Value;
        if (healHpPercent.HasValue) cfg.HealHpPercent = (float)Math.Clamp(healHpPercent.Value, 0d, 1d);
        if (healingItemIds != null) cfg.HealingItemIds = ParseInts(healingItemIds).ToList();
        if (desiredJob.HasValue) cfg.DesiredJobId = desiredJob.Value;
        if (verbose.HasValue) cfg.Verbose = verbose.Value;
        if (winMargin.HasValue) cfg.WinMargin = Math.Clamp(winMargin.Value, 0d, 2d);
        if (maxRoundsToKill.HasValue) cfg.MaxRoundsToKill = Math.Max(1, maxRoundsToKill.Value);
        if (maxTargetHp.HasValue) cfg.MaxTargetHp = Math.Max(0, maxTargetHp.Value);
        if (restWhenNoPotions.HasValue) cfg.RestWhenNoPotions = restWhenNoPotions.Value;
        if (restBelowPercent.HasValue) cfg.RestBelowPercent = (float)Math.Clamp(restBelowPercent.Value, 0d, 1d);
        if (autoEscapeWhenStuck.HasValue) cfg.AutoEscapeWhenStuck = autoEscapeWhenStuck.Value;
        if (!string.IsNullOrWhiteSpace(partyRole))
        {
            if (Enum.TryParse<PartyRole>(partyRole, ignoreCase: true, out var role))
                cfg.PartyRole = role;
            else
                return $"Unknown partyRole '{partyRole}'. Valid: Auto, Tank, Dps, Healer, Buffer, Utility.";
        }
        if (itemScript != null) cfg.ItemScript = itemScript;
        if (announceTarget.HasValue) cfg.AnnounceTargetInChat = announceTarget.Value;
        if (listenToPartyChat.HasValue) cfg.ListenToPartyChat = listenToPartyChat.Value;
        bots.SaveConfig(botId); // persist so the change survives a stop+respawn / dashboard restart
        return $"Updated {botId}.";
    }

    [McpServerTool(Name = "say"),
     Description("Make a bot speak in map chat. Used by a squad leader to drive followers via the party-command grammar — '!engage <classId>', '!flee', '!hold', '!regroup', '!stop', '!hunt'/'!follow'. Followers only obey chat from their SquadLeaderName (or party leader name when unsquadded). The bot itself ignores its own echo. Returns the line sent.")]
    public static async Task<object> Say(BotManager bots, string botId, string text)
    {
        var s = bots.GetSession(botId);
        if (s == null) return new { error = $"No bot '{botId}'." };
        if (string.IsNullOrWhiteSpace(text)) return new { error = "Empty text." };
        await s.SayAsync(text);
        return new { sent = text };
    }

    [McpServerTool(Name = "set_item_script"),
     Description("Set the bot's item-usage rule script (mirrors set_skill_script, but for inventory consumables — potions, Fly Wing, Butterfly Wing). Grammar: `use <itemId> [if <cond>[ and <cond>]*] [every <seconds>]`. LHS: hppct, sppct, stuck (seconds idle), enemies (count), dead (1/0). Ops: < <= > >= == !=. Example: `use 501 if hppct < 50\\nuse 601 if stuck >= 8 every 8`. Persisted; survives stop+respawn.")]
    public static string SetItemScript(BotManager bots, string botId, string script)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return $"No bot '{botId}'.";
        cfg.ItemScript = script ?? "";
        bots.SaveConfig(botId);
        return $"Item script set ({(cfg.ItemScript.Length)} chars).";
    }

    [McpServerTool(Name = "get_item_script"),
     Description("Read the bot's current item-usage rule script and any parse diagnostics.")]
    public static object GetItemScript(BotManager bots, string botId)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return new { error = $"No bot '{botId}'." };
        var brain = bots.GetBehavior(botId);
        return new { script = cfg.ItemScript ?? "", diagnostics = brain?.LastItemScriptDiagnostics ?? "" };
    }

    [McpServerTool(Name = "allocate_stats"),
     Description("Spend stat points on a bot. Values are deltas to ADD to each stat. The server validates affordability and the 99 cap, so an over-spend is a no-op.")]
    public static async Task<string> AllocateStats(BotManager bots, string botId,
        int str = 0, int agi = 0, int vit = 0, int intel = 0, int dex = 0, int luk = 0)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        var before = DateTime.UtcNow;
        await s.ApplyStatPointsAsync(new[] { str, agi, vit, intel, dex, luk });
        return await WithErrorReadback(s, before, $"Requested stat allocation on {botId}: +{str}STR +{agi}AGI +{vit}VIT +{intel}INT +{dex}DEX +{luk}LUK.");
    }

    [McpServerTool(Name = "allocate_skill"),
     Description("Spend one skill point to raise a skill on a bot. 'skill' is the CharacterSkill enum name (e.g. 'BasicMastery') or its numeric id. Server validates points/prereqs/max.")]
    public static async Task<string> AllocateSkill(BotManager bots, string botId, string skill)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        if (!TryParseSkill(skill, out var sk)) return $"Unknown skill '{skill}'.";
        var before = DateTime.UtcNow;
        await s.ApplySkillPointAsync(sk);
        return await WithErrorReadback(s, before, $"Requested skill point into {sk} on {botId}.");
    }

    [McpServerTool(Name = "equip_item"), Description("Equip an inventory item on a bot by its bag id.")]
    public static async Task<string> EquipItem(BotManager bots, string botId, int bagId)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        var before = DateTime.UtcNow;
        await s.EquipItemAsync(bagId);
        return await WithErrorReadback(s, before, $"Requested equip of bag {bagId} on {botId}.");
    }

    [McpServerTool(Name = "unequip_item"), Description("Unequip an item on a bot by its bag id.")]
    public static async Task<string> UnequipItem(BotManager bots, string botId, int bagId)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        var before = DateTime.UtcNow;
        await s.UnequipItemAsync(bagId);
        return await WithErrorReadback(s, before, $"Requested unequip of bag {bagId} on {botId}.");
    }

    [McpServerTool(Name = "force_sell"),
     Description("Force a bot to head to the nearest town shop now and sell its junk/common gear, ignoring the weight/stack thresholds and shop cooldown. Works even if AutoShop is off.")]
    public static string ForceSell(BotManager bots, string botId)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return $"No bot '{botId}'.";
        b.RequestSell();
        return $"{botId} will head to a shop to sell.";
    }

    [McpServerTool(Name = "go_to"),
     Description("Send a bot to a map + coordinate and hold there until released (overrides hunting/shopping). Release with resume_bot.")]
    public static string GoTo(BotManager bots, string botId, string map, int x, int y)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return $"No bot '{botId}'.";
        b.GoToAndWait(map, x, y);
        return $"{botId} ordered to {map} ({x},{y}) and will hold there.";
    }

    [McpServerTool(Name = "resume_bot"),
     Description("Release a bot from a go_to hold and resume normal autonomous behavior.")]
    public static string ResumeBot(BotManager bots, string botId)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return $"No bot '{botId}'.";
        b.ClearPark();
        return $"{botId} released; resuming.";
    }

    [McpServerTool(Name = "use_skill"),
     Description("Manually cast a skill on a bot. target = 'enemy' (single-target — uses the bot's current target if targetId is 0; also valid for Ally-target skills with a friendly entity id), 'self' (buff/self-heal), or 'ground' (uses x,y). 'skill' = CharacterSkill name (e.g. 'FireBolt') or numeric id; 'level' = 0 for the bot's max known level.")]
    public static async Task<string> UseSkill(BotManager bots, string botId, string skill, int level = 0,
        string target = "enemy", int targetId = 0, int x = 0, int y = 0)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        if (!TryParseSkill(skill, out var sk)) return $"Unknown skill '{skill}'.";

        var before = DateTime.UtcNow;
        switch (target.ToLowerInvariant())
        {
            case "self":
                await s.UseSkillSelfAsync(sk, level);
                break;
            case "ground":
                await s.UseSkillGroundAsync(sk, level, x, y);
                break;
            default:
                if (targetId == 0)
                {
                    var b = bots.GetBehavior(botId);
                    if (b != null) targetId = b.TargetId;
                    if (targetId == 0) return $"No targetId given and {botId} has no current target.";
                }
                await s.UseSkillOnTargetAsync(sk, level, targetId);
                break;
        }
        return await WithErrorReadback(s, before, $"Cast {sk} L{level} ({target}).");
    }

    [McpServerTool(Name = "queue_skill"),
     Description("Enqueue a skill cast to fire as soon as the bot's tick loop reaches it (fire-and-forget; returns immediately, no 300ms readback wait like use_skill). The bot logs both the enqueue and the execute (with queue-to-execute latency). Same target semantics as use_skill: target='enemy' (uses current committed target if targetId=0) | 'self' | 'ground' (uses x,y) | 'ally' (most-injured visible player if targetId=0). Subject to the same throttles as the skill script (0.5s global, 1.5s per skill) — agents can queue ahead without overrunning the server's cast rate.")]
    public static object QueueSkill(BotManager bots, string botId, string skill, int level = 0,
        string target = "enemy", int targetId = 0, int x = 0, int y = 0)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        if (!TryParseSkill(skill, out var sk)) return new { error = $"Unknown skill '{skill}'." };
        var castTarget = target.ToLowerInvariant() switch
        {
            "self" => BotBehavior.SkillCastTarget.SelfCast,
            "ground" => BotBehavior.SkillCastTarget.Ground,
            "ally" => BotBehavior.SkillCastTarget.Ally,
            _ => BotBehavior.SkillCastTarget.Enemy,
        };
        var seq = b.QueueSkill(sk, level, castTarget, targetId, x, y);
        return new { seq, queueSize = b.SkillQueueSize, message = $"Queued #{seq}: {sk} L{level} ({castTarget})." };
    }

    [McpServerTool(Name = "clear_skill_queue"),
     Description("Drop every pending queued skill cast on a bot. Use this if the queue's gotten too long, a previous plan is no longer valid, or the bot's state shifted (target died, party changed, etc.). Returns the number of pending casts that were dropped.")]
    public static object ClearSkillQueue(BotManager bots, string botId)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        var cleared = b.ClearSkillQueue();
        return new { cleared };
    }

    [McpServerTool(Name = "get_skill_queue"),
     Description("Read a bot's pending skill queue (FIFO). Each entry: { seq, skill, level, target, targetId, x, y, queuedAt, ageMs }. Useful to decide whether to wait, clear, or queue more.")]
    public static object GetSkillQueue(BotManager bots, string botId)
    {
        var b = bots.GetBehavior(botId);
        if (b == null) return new { error = $"No bot '{botId}'." };
        var q = b.PeekSkillQueue();
        var now = DateTime.UtcNow;
        return new
        {
            count = q.Count,
            items = q.Select(e => new
            {
                e.Seq,
                skill = e.Skill.ToString(),
                e.Level,
                target = e.Target.ToString(),
                e.TargetId,
                e.X,
                e.Y,
                queuedAt = e.QueuedAt,
                ageMs = (int)(now - e.QueuedAt).TotalMilliseconds,
            }).ToList(),
        };
    }

    [McpServerTool(Name = "set_skill_script"),
     Description("Replace a bot's skill rule script (see SKILL_SCRIPT.md). One rule per line: 'use <Skill> <level> [self|enemy|ground] [every <sec>] [if <cond> and <cond>...]'. Returns the number of rules parsed plus any per-line errors. The script applies live; an empty script disables auto-cast.")]
    public static object SetSkillScript(BotManager bots, string botId, string script)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return new { error = $"No bot '{botId}'." };
        var (count, errors) = BotBehavior.ValidateSkillScript(script ?? "");
        cfg.SkillScript = script ?? "";
        return new { rules = count, errors };
    }

    [McpServerTool(Name = "get_skill_script"),
     Description("Read a bot's current skill rule script.")]
    public static object GetSkillScript(BotManager bots, string botId)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return new { error = $"No bot '{botId}'." };
        return new { script = cfg.SkillScript ?? "" };
    }

    [McpServerTool(Name = "list_skills"),
     Description("List a bot's currently known + granted skills (name + level). Useful when writing a skill script so you only reference what the bot can actually cast.")]
    public static object ListSkills(BotManager bots, string botId)
    {
        var s = bots.GetSession(botId);
        if (s == null) return new { error = $"No bot '{botId}'." };
        return s.WithState(w => new
        {
            known = w.Self.KnownSkills.Select(k => new { skill = k.Skill.ToString(), k.Level }).ToList(),
            granted = w.Self.GrantedSkills.Select(k => new { skill = k.Skill.ToString(), k.Level }).ToList(),
        });
    }

    [McpServerTool(Name = "create_party"),
     Description("Have a bot create a party with the given name (the server requires Basic Mastery level 6+ to organize a party). Optionally invite a nearby entity immediately on success.")]
    public static async Task<string> CreateParty(BotManager bots, string botId, string partyName, int inviteEntityId = 0)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        var before = DateTime.UtcNow;
        await s.CreatePartyAsync(partyName, inviteEntityId);
        return await WithErrorReadback(s, before, $"{botId} requested party '{partyName}'.");
    }

    [McpServerTool(Name = "invite_to_party"),
     Description("Invite someone to the bot's party (the bot must be the party leader). 'target' is either a numeric entity id or a player name.")]
    public static async Task<string> InviteToParty(BotManager bots, string botId, string target)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        var before = DateTime.UtcNow;
        if (int.TryParse(target, out var id) && id > 0)
            await s.InvitePartyMemberAsync(id);
        else
            await s.InvitePartyMemberByNameAsync(target);
        return await WithErrorReadback(s, before, $"{botId} invited '{target}' to the party.");
    }

    [McpServerTool(Name = "accept_party_invite"),
     Description("Accept a pending party invite on a bot. If partyId is 0, accepts the most recent pending invite (see get_party). When no invite is pending the result is { error: \"...\" } rather than a success string, so the dashboard can flag it.")]
    public static async Task<object> AcceptPartyInvite(BotManager bots, string botId, int partyId = 0)
    {
        var s = bots.GetSession(botId);
        if (s == null) return new { error = $"No bot '{botId}'." };
        if (partyId == 0)
        {
            var pending = s.GetPendingPartyInvite();
            if (pending == null) return new { error = $"{botId} has no pending party invite." };
            partyId = pending.PartyId;
        }
        var before = DateTime.UtcNow;
        await s.AcceptPartyInviteAsync(partyId);
        return await WithErrorReadback(s, before, $"{botId} accepted party invite (id {partyId}).");
    }

    [McpServerTool(Name = "ensure_basic_mastery"),
     Description("Spend the bot's unspent skill points into Basic Mastery until it reaches the given level (default 6 — the inviter threshold for parties; level 4 is the invitee threshold). No-op if already at level or out of points. Returns { spent, error } where 'spent' is the number of points actually applied and 'error' is the latest server error since the call started (null if none).")]
    public static async Task<object> EnsureBasicMastery(BotManager bots, string botId, int level = 6)
    {
        var s = bots.GetSession(botId);
        if (s == null) return new { error = $"No bot '{botId}'." };
        var before = DateTime.UtcNow;
        var spent = await s.EnsureSkillAsync(CharacterSkill.BasicMastery, level);
        return new { spent, error = s.LatestErrorAfter(before) };
    }

    [McpServerTool(Name = "leave_party"),
     Description("Leave the bot's current party.")]
    public static async Task<string> LeaveParty(BotManager bots, string botId)
    {
        var s = bots.GetSession(botId);
        if (s == null) return $"No bot '{botId}'.";
        var before = DateTime.UtcNow;
        await s.LeavePartyAsync();
        return await WithErrorReadback(s, before, $"{botId} left the party.");
    }

    [McpServerTool(Name = "get_party"),
     Description("Read the bot's party state: whether it's in a party, whether it's the leader, the leader's character name (empty when this bot IS the leader), and the latest pending invite. Followers use 'leaderName' to find the leader in their EntityView (and the minimap broadcast when off-screen).")]
    public static object GetParty(BotManager bots, string botId)
    {
        var s = bots.GetSession(botId);
        if (s == null) return new { error = $"No bot '{botId}'." };
        var pending = s.GetPendingPartyInvite();
        object? invite = pending == null
            ? null
            : new { pending.PartyId, pending.PartyName, pending.SenderName, atUtc = pending.AtUtc };
        return new
        {
            inParty = s.InParty,
            isLeader = s.IsPartyLeader,
            leaderName = s.PartyLeaderName,
            pendingInvite = invite,
        };
    }

    // Give the server a moment to push back a rejection, so a fire-and-forget action the server refused
    // (bad skill prereq, can't equip here, not enough zeny, etc.) doesn't read back as a success.
    private static async Task<string> WithErrorReadback(BotSession s, DateTime before, string okMessage)
    {
        await Task.Delay(300);
        var err = s.LatestErrorAfter(before);
        return err == null ? okMessage : $"{okMessage} NOTE: the server reported an error: {err}";
    }

    private static object? ConfigView(BotBehaviorConfig? c) => c == null ? null : new
    {
        c.Enabled, c.HomeMap, c.AutoLoot, c.AutoShop, c.Verbose, c.DesiredJobId,
        c.RestWhenNoPotions, c.RestBelowPercent,
        partyRole = c.PartyRole.ToString(),
        c.WinMargin, c.MaxRoundsToKill, c.MaxTargetHp, c.HealHpPercent,
        healingItemIds = c.HealingItemIds, lootBlacklist = c.LootBlacklist.ToList(),
        itemScriptLength = (c.ItemScript ?? "").Length,
        squadId = c.SquadId, c.IsSquadLeader, c.SquadLeaderName,
        c.AnnounceTargetInChat, c.ListenToPartyChat,
    };

    private static void ResolveCodes(GameDatabase db, HashSet<int> set, string? codes)
    {
        if (string.IsNullOrWhiteSpace(codes)) return;
        foreach (var raw in codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (db.ClassIdOf(raw) is int id) set.Add(id);
    }

    private static IEnumerable<int> ParseInts(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Where(s => int.TryParse(s, out _)).Select(int.Parse);

    private static bool TryParseSkill(string s, out CharacterSkill skill)
    {
        if (int.TryParse(s, out var n) && Enum.IsDefined(typeof(CharacterSkill), n)) { skill = (CharacterSkill)n; return true; }
        return Enum.TryParse(s, true, out skill);
    }
}
