using RebuildSharedData.Enum;
using RebuildSharedData.ClientTypes;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Behavior;

// Phase C: auto-shopping. When overweight / inventory-heavy / low on potions, travel to a reachable
// tool dealer, drive the NPC conversation (advance dialogs, pick the buy/sell menu option), sell junk
// per ShopPolicy and/or restock potions, then travel home. Hard caps (deadline + step cap) guarantee the
// trip always ends and the bot resumes hunting rather than getting stuck in a dialog tree.
public sealed partial class BotBehavior
{
    private enum ShopPhase { None, Travel, Approach, Converse, ReturnTravel }
    private enum ShopGoal { Sell, Buy }

    private ShopPhase _shopPhase = ShopPhase.None;
// Why the current trip exists, for the dashboard ("buy Rapier (weapon upgrade, atk 70)", "sell loot + restock Fly Wing").
private string _shopPurpose = "";
private string? _customBuyWhy; // set by the gear logic alongside RequestBuyItems
    private readonly Queue<ShopGoal> _shopGoals = new();
    private ShopGoal _shopGoal;
    private bool _shopOrderSent;
    private string _shopMap = "";
    private int _shopX, _shopY;
    private string _shopNpcName = "";
    private int _shopSteps;
    private DateTime _shopDeadline;
    private DateTime _shopNextAction;
    private DateTime _shopCooldownUntil = DateTime.MinValue;

    // One-shot custom manifests set by MCP buy_item / sell_item. When either is set, StartShopTrip takes a
    // dedicated path that bypasses the auto-restock+auto-sell-junk logic. Cleared by EndShopTrip.
    private List<(int itemId, int qty)>? _customBuyItems;
    private List<(int itemId, int qty)>? _customSellItems;

    // Passive sell queue: items the bot will INCLUDE in the next natural auto-shop sell trip alongside
    // its ShopPolicy junk. Doesn't force a shop trip on its own — different from _customSellItems which
    // is "go shop NOW and sell only this". Cleared on successful sell. Operator can queue items via MCP
    // ahead of time so the next time the bot naturally shops, they go with it.
    private readonly List<(int itemId, int qty)> _queuedSellItems = new();
    private readonly object _queuedSellLock = new();

    // Pending drop queue: ground-drop requests issued via MCP. Processed once per tick from the main
    // FSM loop (we don't drop synchronously from the MCP thread — the bot's session is single-threaded
    // and we want the drop to fail cleanly if the bot dies / disconnects between request and execution).
    private readonly Queue<(int itemId, int qty)> _pendingDrops = new();
    private readonly object _pendingDropsLock = new();

    /// <summary>Queue items to drop on the ground on the next tick. Resolved by item id; if multiple
    /// stacks share that id, the lowest bag id is drained first. Equipped stacks are skipped.</summary>
    public void RequestDropItems(IEnumerable<(int itemId, int qty)> items)
    {
        lock (_pendingDropsLock)
        {
            foreach (var (id, q) in items)
                if (id > 0 && q > 0) _pendingDrops.Enqueue((id, q));
        }
    }

    /// <summary>Add items to the sell queue. Qty &lt;= 0 means "sell every stack of this id on the next
    /// shop trip". Doesn't trigger a shop trip on its own — waits for the natural trigger (weight,
    /// stack count, junk count). Use <see cref="RequestSellItems"/> to force one immediately.</summary>
    public void QueueSellItems(IEnumerable<(int itemId, int qty)> items)
    {
        lock (_queuedSellLock)
        {
            foreach (var (id, q) in items)
                if (id > 0) _queuedSellItems.Add((id, q));
        }
    }

    /// <summary>Snapshot of the current sell queue — what'll go on the next shop trip alongside junk.</summary>
    public IReadOnlyList<(int itemId, int qty)> GetSellQueue()
    {
        lock (_queuedSellLock)
            return _queuedSellItems.ToArray();
    }

    /// <summary>Clear the sell queue. No-op if empty.</summary>
    public void ClearSellQueue()
    {
        lock (_queuedSellLock)
            _queuedSellItems.Clear();
    }

    // Per-item restock targets: itemId -> minimum count to maintain. Distinct from the single-item
    // _config.RestockItemId / RestockBelow / RestockTargetCount which only handles one potion. The list
    // here is operator-set via MCP set_restock and processed opportunistically on EVERY shop trip — at
    // whatever NPC the bot reaches, any list item the NPC stocks gets topped up to its target (limited
    // by stock + zeny). Also a Buy-goal trigger: if any item is below target AND a reachable trader
    // sells any of them, ShouldStartShopTrip allows the trip.
    private readonly Dictionary<int, int> _restockTargets = new();
    private readonly object _restockLock = new();

    /// <summary>Set the target stock count for an item. <paramref name="targetCount"/> &lt;= 0 removes
    /// the entry (no longer restocked). The next shop trip the bot takes will buy enough of this item
    /// at any visited NPC that sells it to top up to <paramref name="targetCount"/>.</summary>
    public void SetRestockTarget(int itemId, int targetCount)
    {
        if (itemId <= 0) return;
        lock (_restockLock)
        {
            if (targetCount <= 0) _restockTargets.Remove(itemId);
            else _restockTargets[itemId] = targetCount;
        }
    }

    /// <summary>Snapshot of the current restock targets (itemId -> target count).</summary>
    public IReadOnlyDictionary<int, int> GetRestockTargets()
    {
        lock (_restockLock)
            return new Dictionary<int, int>(_restockTargets);
    }

    /// <summary>Drop all restock-target entries.</summary>
    public void ClearRestockTargets()
    {
        lock (_restockLock)
            _restockTargets.Clear();
    }

    /// <summary>Inventory count of <paramref name="itemId"/> across all stacks (equipped excluded).</summary>
    private int CountInInventory(int itemId) => _bot.WithState(w =>
    {
        var equipped = new HashSet<int>();
        foreach (var b in w.Self.EquippedBagIds) if (b != 0) equipped.Add(b);
        var n = 0;
        foreach (var it in w.Self.Inventory)
            if (it.ItemId == itemId && !equipped.Contains(it.BagId)) n += it.Count;
        return n;
    });

    /// <summary>True when the restock list has any item below its target AND at least one reachable
    /// trader sells one of those shortfall items. Used by ShouldStartShopTrip as an extra trigger.</summary>
    private bool RestockListNeedsShopping()
    {
        if (_data == null) return false;
        Dictionary<int, int> snapshot;
        lock (_restockLock)
        {
            if (_restockTargets.Count == 0) return false;
            snapshot = new Dictionary<int, int>(_restockTargets);
        }
        var shortfalls = new HashSet<int>();
        foreach (var (id, target) in snapshot)
            if (CountInInventory(id) < target) shortfalls.Add(id);
        if (shortfalls.Count == 0) return false;
        // Any reachable trader stocks any shortfall item?
        foreach (var npc in _data.Npcs)
        {
            if (!npc.IsTrader || npc.SellsItems == null) continue;
            var stocksOne = false;
            foreach (var s in shortfalls)
                if (npc.SellsItems.Contains(s)) { stocksOne = true; break; }
            if (!stocksOne) continue;
            if (_data.HopCount(_bot.WithState(w => w.Self.Map), npc.Map) >= 0) return true;
        }
        return false;
    }

    /// <summary>Pop one drop request (if any) and execute it. Item is matched by id in the bot's current
    /// inventory; if multiple stacks share an id, the LOWEST bag id is drained first. Equipped stacks
    /// are skipped (the server rejects them anyway). One drop per tick — keeps the rate of DropItem
    /// packets to ~one per second, well below any anti-spam limit.</summary>
    private async Task DrainPendingDropAsync(CancellationToken ct)
    {
        (int itemId, int qty) req;
        lock (_pendingDropsLock)
        {
            if (_pendingDrops.Count == 0) return;
            req = _pendingDrops.Dequeue();
        }
        if (_data == null) return;

        // Find a (bagId, available) we can pull from.
        var match = _bot.WithState(w =>
        {
            var equipped = new HashSet<int>();
            foreach (var b in w.Self.EquippedBagIds) if (b != 0) equipped.Add(b);
            (int bagId, int count) best = (0, 0);
            foreach (var it in w.Self.Inventory)
            {
                if (it.ItemId != req.itemId) continue;
                if (equipped.Contains(it.BagId)) continue;
                if (best.bagId == 0 || it.BagId < best.bagId) best = (it.BagId, it.Count);
            }
            return best;
        });
        if (match.bagId == 0)
        {
            OnLog?.Invoke($"Drop request: no unequipped stack of item #{req.itemId} in inventory.");
            return;
        }
        var drop = Math.Min(req.qty, match.count);
        OnLog?.Invoke($"Dropping {drop}x {_data.ItemName(req.itemId)} (#{req.itemId}, bag {match.bagId}).");
        await _bot.DropItemAsync(match.bagId, drop, ct);

        // If the request asked for more than the matched stack, re-queue the remainder so the next tick
        // picks up the next stack of the same id.
        if (drop < req.qty)
        {
            lock (_pendingDropsLock)
                _pendingDrops.Enqueue((req.itemId, req.qty - drop));
        }
    }

    private bool ShopTripActive => _shopPhase != ShopPhase.None;

    /// <summary>Queue a one-shot custom buy at the nearest trader that stocks the first item in the list.
    /// Triggers a shop trip on the next tick that overrides the auto-restock loop.</summary>
    public void RequestBuyItems(IEnumerable<(int itemId, int qty)> items)
    {
        var list = new List<(int, int)>();
        foreach (var (id, q) in items)
            if (id > 0 && q > 0) list.Add((id, q));
        if (list.Count == 0) return;
        _customBuyItems = list;
        _forceShop = true;
        _shopCooldownUntil = DateTime.MinValue;
    }

    /// <summary>Queue a one-shot custom sell of the listed items (qty &lt;= 0 = sell every stack of that id).
    /// Equipped items are skipped automatically; equipped bag ids are filtered out at order-build time.</summary>
    public void RequestSellItems(IEnumerable<(int itemId, int qty)> items)
    {
        var list = new List<(int, int)>();
        foreach (var (id, q) in items)
            if (id > 0) list.Add((id, q));
        if (list.Count == 0) return;
        _customSellItems = list;
        _forceShop = true;
        _shopCooldownUntil = DateTime.MinValue;
    }

    private (int weight, int maxWeight, int stacks, int restockHeld, int zeny) ReadShopState()
    {
        var st = ReadShopStateRaw();
        RefreshFlyWingPool(st.maxWeight);
        return st;
    }

    // Fly Wing pool: enough for travel shortcuts plus ambush escapes, never more than 3% of max weight so the
    // wings themselves don't pull the next sell trip forward. Only bought on trips already happening.
    // ponytail: fixed 3% cap; derive from the bot's typical loot weight if wings ever crowd out loot.
    private const double FlyWingPoolWeightShare = 0.03;
    private void RefreshFlyWingPool(int maxWeight)
    {
        if (_config.FlyWingPoolSize <= 0 || _data == null || maxWeight <= 0) return;
        var each = Math.Max(1, _data.UsableItemWeight(_config.FlyWingItemId));
        SetRestockTarget(_config.FlyWingItemId, Math.Min(_config.FlyWingPoolSize, (int)(maxWeight * FlyWingPoolWeightShare / each)));
    }

    private (int weight, int maxWeight, int stacks, int restockHeld, int zeny) ReadShopStateRaw()
        => _bot.WithState(w =>
        {
            var held = 0;
            if (_config.RestockItemId > 0)
                foreach (var it in w.Self.Inventory)
                    if (it.ItemId == _config.RestockItemId) held += it.Count;
            return (w.Self.Weight, w.Self.MaxWeight, w.Self.Inventory.Count, held, w.Self.Zeny);
        });

    private bool ShouldStartShopTrip(Snapshot snap)
    {
        if (!_config.AutoShop || _data == null) return false;
        if (DateTime.UtcNow < _shopCooldownUntil) return false;
        var st = ReadShopState();
        // A wing restock alone isn't worth a trip unless spare zeny buys at least half the shortfall.
        var restockHeld = st.restockHeld;
        if (IsConvenienceItem(_config.RestockItemId) && restockHeld < _config.RestockBelow
            && _data!.UsableItemPrice(_config.RestockItemId) is var price and > 0
            && ConvenienceBudget(st.zeny, 0) / price < (_config.RestockTargetCount - restockHeld + 1) / 2)
            restockHeld = int.MaxValue;
        return ShopPolicy.ShouldStartTrip(st.weight, st.maxWeight, restockHeld, _config, CountSaleableItems);
    }

    /// <summary>Force an immediate shop trip to restock items on the restock-targets list. No-op when
    /// the list is empty or every item is at/above target. Operator calls this via MCP when they want
    /// the bot to top up before continuing — bypasses the natural sell-trip threshold.</summary>
    public bool ForceRestockTrip()
    {
        if (_data == null) return false;
        Dictionary<int, int> snapshot;
        lock (_restockLock) snapshot = new Dictionary<int, int>(_restockTargets);
        if (snapshot.Count == 0) return false;
        var anyShortfall = false;
        foreach (var (id, target) in snapshot)
            if (CountInInventory(id) < target) { anyShortfall = true; break; }
        if (!anyShortfall) return false;
        _forceShop = true;
        _shopCooldownUntil = DateTime.MinValue;
        return true;
    }

    /// <summary>Count inventory items the auto-sell policy would actually sell — used by the junk
    /// trigger so we don't shop on (e.g.) a bot carrying lots of cards / refined gear.</summary>
    private int CountSaleableItems() => _bot.WithState(w =>
    {
        if (_data == null) return 0;
        var equipped = new HashSet<int>();
        foreach (var bagId in w.Self.EquippedBagIds) if (bagId != 0) equipped.Add(bagId);
        var n = 0;
        foreach (var it in w.Self.Inventory)
        {
            var def = _data.Item(it.ItemId);
            if (ShopPolicy.ShouldSell(def, it, equipped.Contains(it.BagId), _config)) n++;
        }
        return n;
    });

    private void StartShopTrip(Snapshot snap, bool force = false)
    {
        // Custom one-shot manifests (from MCP buy_item / sell_item) take priority over the auto-shop logic.
        if (_customBuyItems != null || _customSellItems != null)
        {
            var requireItem = _customBuyItems != null && _customBuyItems.Count > 0 ? _customBuyItems[0].itemId : 0;
            if (!ResolveShop(snap.Map, requireItem))
            {
                OnLog?.Invoke(requireItem > 0
                    ? $"Custom shop: no reachable trader stocks item #{requireItem} — aborting."
                    : "Custom shop: no reachable trader — aborting.");
                _customBuyItems = null;
                _customSellItems = null;
                _shopCooldownUntil = DateTime.UtcNow.AddSeconds(60);
                return;
            }
            _shopGoals.Clear();
            if (_customSellItems != null) _shopGoals.Enqueue(ShopGoal.Sell);
            if (_customBuyItems != null) _shopGoals.Enqueue(ShopGoal.Buy);
            _shopGoal = _shopGoals.Dequeue();
            _shopOrderSent = false;
            _shopSteps = 0;
            _shopPhase = ShopPhase.Travel;
            _shopDeadline = ShopTripDeadline();
            _shopNextAction = DateTime.MinValue;
            var customWhat = _customSellItems != null && _customBuyItems != null ? "sell + buy"
                : _customSellItems != null ? "sell" : "buy";
            OnLog?.Invoke($"Custom shop trip: heading to {_data!.MapName(_shopMap)} ({_shopMap}) to {customWhat} at '{_shopNpcName}'.");
            var parts = new List<string>();
            if (_customBuyItems != null)
                parts.Add("buy " + string.Join(", ", _customBuyItems.Select(b => (b.qty > 1 ? $"{b.qty}x " : "") + _data.ItemName(b.itemId)))
                          + (_customBuyWhy != null ? $" ({_customBuyWhy})" : ""));
            if (_customSellItems != null) parts.Add($"sell {_customSellItems.Count} item type(s)");
            _shopPurpose = string.Join(" + ", parts);
            _customBuyWhy = null;
            return;
        }

        var st = ReadShopState();
        var saleable = CountSaleableItems();
        var needSell = force || ShopPolicy.ShouldStartTrip(st.weight, st.maxWeight, int.MaxValue, _config, () => saleable);
        // Build the "needed for buy" item set: the single-item config restock (when below its low
        // threshold) + every restock-list item below its target. The list items don't trigger a trip
        // on their own (ShouldStartShopTrip ignores them) — but once a trip IS happening, the bot
        // should opportunistically top them up.
        var needBuyAny = new List<int>();
        if (_config.RestockItemId > 0 && st.restockHeld < _config.RestockBelow)
            needBuyAny.Add(_config.RestockItemId);
        Dictionary<int, int> restockSnapshot;
        lock (_restockLock) restockSnapshot = new Dictionary<int, int>(_restockTargets);
        foreach (var (id, target) in restockSnapshot)
            if (CountInInventory(id) < target && !needBuyAny.Contains(id)) needBuyAny.Add(id);
        var needBuy = needBuyAny.Count > 0;
        if (!needSell && !needBuy) return;
        needSell |= saleable > 0; // already making the trip: sell whatever there is

        // A buy goal requires a trader that stocks at least one needed item. Prefer such a trader so
        // sell + buy happen at the same NPC; if none is reachable, fall back to any trader for selling.
        var resolved = false;
        if (needBuy) resolved = ResolveShop(snap.Map, needBuyAny);
        if (!resolved)
        {
            needBuy = false;
            if (needSell) resolved = ResolveShop(snap.Map, Array.Empty<int>());
        }
        if (!resolved)
        {
            OnLog?.Invoke("AutoShop: no suitable shop reachable — skipping (will retry later).");
            _shopCooldownUntil = DateTime.UtcNow.AddSeconds(300); // long back-off so we don't loop
            return;
        }

        _shopGoals.Clear();
        if (needSell) _shopGoals.Enqueue(ShopGoal.Sell);
        if (needBuy) _shopGoals.Enqueue(ShopGoal.Buy);

        _shopGoal = _shopGoals.Dequeue();
        _shopOrderSent = false;
        _shopSteps = 0;
        _shopPhase = ShopPhase.Travel;
        _shopDeadline = ShopTripDeadline();
        _shopNextAction = DateTime.MinValue;
        var what = needSell && needBuy ? "sell + restock" : needSell ? "sell" : "restock";
        OnLog?.Invoke($"AutoShop: heading to {_data!.MapName(_shopMap)} ({_shopMap}) to {what} at '{_shopNpcName}'.");
        var restockNames = needBuy ? string.Join(", ", needBuyAny.Select(_data.ItemName)) : "";
        _shopPurpose = needSell && needBuy ? $"sell loot + restock {restockNames}"
            : needSell ? "sell loot" : $"restock {restockNames}";
    }

    // Pick the NEAREST reachable trader by warp-hop distance. When <paramref name="requireAnyOf"/>
    // is non-empty, the trader must sell at least one of those item ids (most town tool dealers also
    // buy junk so they cover selling too). Pass an empty list / null for "any trader works".
    private bool ResolveShop(string fromMap, IReadOnlyCollection<int> requireAnyOf)
    {
        if (_data == null) return false;
        var traders = _data.Npcs.Where(npc => npc.IsTrader
                && (string.IsNullOrEmpty(_config.ShopMap) || npc.Map.Equals(_config.ShopMap, StringComparison.OrdinalIgnoreCase))
                && (requireAnyOf == null || requireAnyOf.Count == 0 || (npc.SellsItems != null && requireAnyOf.Any(npc.SellsItems.Contains))))
            .ToList();

        // A Butterfly Wing drops us in the save town; worth it when that makes the whole trip shorter.
        var saveMap = HasItem(_config.ButterflyWingItemId) && _data.IsUsableItem(_config.ButterflyWingItemId) ? SaveMap() : null;
        if (string.Equals(saveMap, fromMap, StringComparison.OrdinalIgnoreCase)) saveMap = null;

        // Cheap pass by map hops; the few closest go on to the real walking estimate.
        var near = traders
            .Select(npc => (npc, hops: Math.Min(Hops(fromMap, npc.Map), saveMap == null ? int.MaxValue : Hops(saveMap, npc.Map) + 1)))
            .Where(c => c.hops < int.MaxValue).OrderBy(c => c.hops).Take(12).ToList(); // unreachable interiors (ama_in01, xmas_in) used to fill a top 6
        if (near.Count == 0) return false;

        var here = _bot.WithState(w => w.SelfPosition);
        var home = string.IsNullOrEmpty(_config.HomeMap) ? fromMap : _config.HomeMap;
        var homeCell = _data.World.EntryCell(home) is { } entry ? (entry.X, entry.Y) : (here.X, here.Y);
        var landing = saveMap != null && FindAnyWalkableCell(saveMap) is { } land ? (land.X, land.Y) : (0, 0); // ponytail: town centre as the wing landing spot
        NpcEntry? best = null;
        var bestCost = float.MaxValue;
        var bestViaWing = false;
        foreach (var (npc, _) in near)
        {
            var back = WalkCost(npc.Map, npc.X, npc.Y, home, homeCell.Item1, homeCell.Item2);
            if (back < 0) back = 0; // can't estimate the way back: judge on the way there
            var walk = WalkCost(fromMap, here.X, here.Y, npc.Map, npc.X, npc.Y);
            if (walk >= 0 && walk + back < bestCost) { bestCost = walk + back; best = npc; bestViaWing = false; }
            if (saveMap == null) continue;
            var wing = WalkCost(saveMap, landing.Item1, landing.Item2, npc.Map, npc.X, npc.Y);
            if (wing >= 0 && WingCostCells + wing + back < bestCost) { bestCost = WingCostCells + wing + back; best = npc; bestViaWing = true; }
        }
        if (best == null) return false;
        _shopWingTo = bestViaWing ? saveMap : null;
        _shopTripCells = bestCost;
        _shopMap = best.Map;
        _shopX = best.X;
        _shopY = best.Y;
        _shopNpcName = best.Name;
        return true;
    }

    // A fixed 150 s timed out every cross-region walk (Payon from prt_fild08 is ~1200 cells) halfway, then the bot
    // started over. ~0.2 s per estimated cell of the round trip on top.
    private float _shopTripCells;
    private DateTime ShopTripDeadline() => DateTime.UtcNow.AddSeconds(150 + Math.Min(_shopTripCells, 3000) * 0.2);

    private const float WingCostCells = 25; // using the wing + loading the town ≈ walking this many cells

    private int Hops(string from, string to) => _data!.HopCount(from, to) is var hops and >= 0 ? hops : int.MaxValue;

    /// <summary>Route planner cost (walked cells + portal overhead) between two cells, including the last stretch from the
    /// final portal to the target. -1 when unreachable.</summary>
    private float WalkCost(string fromMap, int fromX, int fromY, string toMap, int toX, int toY)
    {
        var plan = PlanRoute(fromMap, fromX, fromY, toMap, toX, toY);
        if (plan == null) return -1;
        var (lastX, lastY) = plan.Edges.Count > 0 ? (plan.Edges[^1].Dx, plan.Edges[^1].Dy) : (fromX, fromY);
        return plan.Cost + Math.Max(Math.Abs(toX - lastX), Math.Abs(toY - lastY));
    }

    private string? _shopWingTo; // save town to Butterfly Wing to at the start of this trip, if that beats walking

    private bool HasItem(int itemId) => itemId > 0 && _bot.WithState(w => w.Self.Inventory.Any(i => i.ItemId == itemId && i.Count > 0));

    /// <summary>The town we respawn in: where we landed after the last death or Butterfly Wing, from saved telemetry.
    /// Null until that happened once.</summary>
    private string? SaveMap()
    {
        string? save = null;
        var wingName = _data?.ItemName(_config.ButterflyWingItemId);
        var returning = false;
        foreach (var e in _bot.Telemetry.Snapshot())
        {
            if (e.Type == RoBotClient.Bot.State.TelemetryEventType.Death
                || (e.Type == RoBotClient.Bot.State.TelemetryEventType.UsedItem && e.Detail == wingName)) returning = true;
            else if (returning && e.Type == RoBotClient.Bot.State.TelemetryEventType.MapChange) { save = e.Detail; returning = false; }
        }
        return save;
    }

    // Single-item overload kept for the custom-buy path that needs an exact item.
    private bool ResolveShop(string fromMap, int requireItemId) =>
        ResolveShop(fromMap, requireItemId > 0 ? new[] { requireItemId } : Array.Empty<int>());

    /// <summary>Whether we stand in the same walkable region (room) as the shop NPC. Unknown regions count as yes.</summary>
    private bool InShopRoom(Snapshot snap)
    {
        if (_data == null || !_data.World.IsBuilt) return true;
        var mine = _data.World.RegionAt(snap.Map, snap.SelfPos.X, snap.SelfPos.Y);
        var shop = _data.World.RegionAt(_shopMap, _shopX, _shopY);
        if (shop == 0 && _data.GetWalkMap(_shopMap) is { } wm && wm.TryFindWalkableNear(_shopX, _shopY, 4, out var nx, out var ny))
            shop = _data.World.RegionAt(_shopMap, nx, ny); // NPCs often stand on a blocked cell (behind a counter)
        return mine == 0 || shop == 0 || mine == shop;
    }

    private async Task TickShopTripAsync(Snapshot snap, bool stuck, CancellationToken ct)
    {
        if (DateTime.UtcNow > _shopDeadline) { await AbortShopAsync("trip timed out", ct); return; }
        // Done buying and walking back is just travel; don't keep showing "Shopping" out in the fields.
        Mode = _shopPhase == ShopPhase.ReturnTravel ? BotMode.Traveling : BotMode.Shopping;

        switch (_shopPhase)
        {
            case ShopPhase.Travel:
                if (string.Equals(snap.Map, _shopMap, StringComparison.OrdinalIgnoreCase) && InShopRoom(snap))
                {
                    _shopPhase = ShopPhase.Approach;
                    _shopNextAction = DateTime.MinValue;
                    return;
                }
                if (_shopWingTo != null)
                {
                    OnLog?.Invoke($"AutoShop: Butterfly Wing to {_data!.MapName(_shopWingTo)} ({_shopWingTo}), closer to {_shopNpcName} than walking.");
                    _shopWingTo = null; // one try; if it fails we just walk
                    _shopNextAction = DateTime.UtcNow.AddSeconds(3);
                    await _bot.UseInventoryItemAsync(_config.ButterflyWingItemId, -1, ct);
                    return;
                }
                if (stuck) { ResetStuck(snap.SelfPos); await NudgeAsync(snap, ct); return; } // wedged en route
                if (DateTime.UtcNow < _shopNextAction) return;
                _shopNextAction = DateTime.UtcNow.AddSeconds(1.3);
                await StepTowardMapAsync(snap, _shopMap, ct, (_shopX, _shopY));
                return;

            case ShopPhase.Approach:
                if (!InShopRoom(snap)) { _shopPhase = ShopPhase.Travel; return; } // wrong room of an indoor map
                var npcId = _bot.WithState(FindNpcEntityId);
                if (npcId != 0) // NPC is in view — the server has no click range check, so click from here
                {
                    _shopOrderSent = false;
                    _shopSteps = 0;
                    _shopPhase = ShopPhase.Converse;
                    _shopNextAction = DateTime.UtcNow.AddMilliseconds(800);
                    await _bot.NpcClickAsync(npcId, ct);
                    return;
                }
                if (stuck) { ResetStuck(snap.SelfPos); await NudgeAsync(snap, ct); return; } // wedged on a building
                if (DateTime.UtcNow < _shopNextAction) return;
                _shopNextAction = DateTime.UtcNow.AddSeconds(1.5);
                await WalkPathTowardAsync(snap, _shopX, _shopY, 16, ct); // A* path around city obstacles
                return;

            case ShopPhase.Converse:
                await TickConverseAsync(snap, ct);
                return;

            case ShopPhase.ReturnTravel:
                if (string.IsNullOrEmpty(_config.HomeMap) ||
                    string.Equals(snap.Map, _config.HomeMap, StringComparison.OrdinalIgnoreCase))
                {
                    EndShopTrip();
                    return;
                }
                if (stuck) { ResetStuck(snap.SelfPos); await NudgeAsync(snap, ct); return; }
                if (DateTime.UtcNow < _shopNextAction) return;
                _shopNextAction = DateTime.UtcNow.AddSeconds(1.3);
                await StepTowardMapAsync(snap, _config.HomeMap, ct);
                return;
        }
    }

    private async Task TickConverseAsync(Snapshot snap, CancellationToken ct)
    {
        if (DateTime.UtcNow < _shopNextAction) return;
        if (++_shopSteps > 30) { await AbortShopAsync("conversation stalled", ct); return; }

        var npc = _bot.SnapshotNpc();
        switch (npc.Phase)
        {
            case NpcPhase.None: // waiting for the first packet after the click
                _shopNextAction = DateTime.UtcNow.AddMilliseconds(600);
                // The server silently drops a click inside its input cooldown, which every walk step starts: the bot clicked
                // right after its last step and waited out the whole conversation ("conversation stalled", 11-16: Bot2 looped
                // on prt_fild05's Tool Dealer every ~2 min). No reply after ~2 s: click again.
                if (_shopSteps % 4 == 0 && _bot.WithState(FindNpcEntityId) is var npcId and not 0)
                    await _bot.NpcClickAsync(npcId, ct);
                return;
            case NpcPhase.Dialog:
                _shopNextAction = DateTime.UtcNow.AddMilliseconds(450);
                await _bot.NpcAdvanceAsync(ct);
                return;
            case NpcPhase.Option:
                var idx = ChooseOption(npc.Options, _shopGoal);
                if (idx < 0) idx = CancelOption(npc.Options); // our shop isn't on this menu — back out
                _shopNextAction = DateTime.UtcNow.AddMilliseconds(450);
                await _bot.NpcSelectOptionAsync(idx, ct);
                return;
            case NpcPhase.ShopBuy:
                await DoBuyAsync(snap, ct);
                return;
            case NpcPhase.ShopSell:
                await DoSellAsync(snap, ct);
                return;
            case NpcPhase.Refine:
                _shopNextAction = DateTime.UtcNow.AddMilliseconds(450);
                await _bot.NpcAdvanceAsync(ct); // leave the refine window
                return;
            case NpcPhase.Ended:
                if (_shopGoals.Count > 0)
                {
                    _shopGoal = _shopGoals.Dequeue();
                    _shopOrderSent = false;
                    _shopSteps = 0;
                    _shopPhase = ShopPhase.Approach; // re-click the same NPC for the next goal
                }
                else
                {
                    _shopPhase = ShopPhase.ReturnTravel;
                    _shopNextAction = DateTime.MinValue;
                    OnLog?.Invoke("Shopping done — heading home.");
                }
                return;
        }
    }

    private async Task DoBuyAsync(Snapshot snap, CancellationToken ct)
    {
        _shopNextAction = DateTime.UtcNow.AddMilliseconds(800);
        if (_shopOrderSent) { await _bot.ShopCloseAsync(ct); return; }
        _shopOrderSent = true;

        var npc = _bot.SnapshotNpc();
        var st = ReadShopState();

        // Custom buy from MCP buy_item — buy what we can afford from the requested manifest.
        if (_customBuyItems != null)
        {
            var lines = new List<(int, int)>();
            var zeny = st.zeny;
            foreach (var (itemId, requestedQty) in _customBuyItems)
            {
                var p = 0;
                foreach (var (id, price) in npc.ShopItems) if (id == itemId) { p = price; break; }
                if (p <= 0) continue; // this NPC doesn't stock it
                var affordable = zeny / p;
                var buy = Math.Min(requestedQty, affordable);
                if (buy <= 0) continue;
                lines.Add((itemId, buy));
                zeny -= buy * p;
                _bot.Telemetry.Record(TelemetryEventType.Bought, snap.SelfLevel, _data?.ItemName(itemId) ?? $"#{itemId}", buy);
            }
            if (lines.Count == 0)
            {
                OnLog?.Invoke("Custom buy: shop doesn't stock the requested item(s), or not enough zeny.");
                await _bot.ShopCloseAsync(ct);
                return;
            }
            OnLog?.Invoke($"Buying {string.Join(", ", lines.Select(l => $"{l.Item2}x {_data?.ItemName(l.Item1) ?? $"#{l.Item1}"}"))}.");
            await _bot.ShopBuySellAsync(lines, ct);
            return;
        }

        // Build a unified buy manifest: the single-item config restock first (back-compat), then
        // every operator-set restock target the NPC stocks. Each item buys enough to top up to target,
        // limited by available zeny (greedy per item — first the legacy RestockItemId, then the list
        // in dictionary order). Single packet per visit.
        var lines2 = new List<(int, int)>();
        var homeFares = ZenyForTripHome(snap);
        var zenyLeft = Math.Max(0, st.zeny - homeFares);
        var convenienceLeft = ConvenienceBudget(st.zeny, homeFares);
        _skillZenyLeft = -1; // new shop trip: the Mammonite pool refills from the purse on its next cast
        var totalCost = 0;

        if (_config.RestockItemId > 0)
        {
            var need = _config.RestockTargetCount - st.restockHeld;
            var defaultPrice = 0;
            foreach (var (id, p) in npc.ShopItems)
                if (id == _config.RestockItemId) { defaultPrice = p; break; }
            if (need > 0 && defaultPrice > 0)
            {
                var affordable = (IsConvenienceItem(_config.RestockItemId) ? Math.Min(zenyLeft, convenienceLeft) : zenyLeft) / defaultPrice;
                var qty = Math.Min(need, affordable);
                if (qty > 0)
                {
                    lines2.Add((_config.RestockItemId, qty));
                    zenyLeft -= qty * defaultPrice;
                    if (IsConvenienceItem(_config.RestockItemId)) convenienceLeft -= qty * defaultPrice;
                    totalCost += qty * defaultPrice;
                    _bot.Telemetry.Record(TelemetryEventType.Bought, snap.SelfLevel,
                        _data?.ItemName(_config.RestockItemId) ?? "", qty);
                }
            }
        }

        Dictionary<int, int> restockSnap;
        lock (_restockLock) restockSnap = new Dictionary<int, int>(_restockTargets);
        foreach (var (itemId, target) in restockSnap)
        {
            if (itemId == _config.RestockItemId) continue; // already handled above
            var price = 0;
            foreach (var (id, p) in npc.ShopItems) if (id == itemId) { price = p; break; }
            if (price <= 0) continue; // this NPC doesn't stock it
            var held = CountInInventory(itemId);
            var need = target - held;
            if (need <= 0) continue;
            var affordable = (IsConvenienceItem(itemId) ? Math.Min(zenyLeft, convenienceLeft) : zenyLeft) / price;
            var qty = Math.Min(need, affordable);
            if (qty <= 0) continue;
            lines2.Add((itemId, qty));
            zenyLeft -= qty * price;
            if (IsConvenienceItem(itemId)) convenienceLeft -= qty * price;
            totalCost += qty * price;
            _bot.Telemetry.Record(TelemetryEventType.Bought, snap.SelfLevel, _data?.ItemName(itemId) ?? $"#{itemId}", qty);
        }

        if (lines2.Count == 0) { await _bot.ShopCloseAsync(ct); return; }
        OnLog?.Invoke($"Restocking: {string.Join(", ", lines2.Select(l => $"{l.Item2}x {_data?.ItemName(l.Item1) ?? $"#{l.Item1}"}"))} ({totalCost}z)"
                      + (homeFares > 0 ? $", keeping {homeFares}z for the Kafra home." : "."));
        await _bot.ShopBuySellAsync(lines2, ct);
    }

    private async Task DoSellAsync(Snapshot snap, CancellationToken ct)
    {
        _shopNextAction = DateTime.UtcNow.AddMilliseconds(800);
        if (_shopOrderSent) { await _bot.ShopCloseAsync(ct); return; }
        _shopOrderSent = true;

        var orders = _bot.WithState(BuildSellOrders); // (bagId, itemId, count)
        if (orders.Count == 0) { await _bot.ShopCloseAsync(ct); return; }

        var lines = new List<(int, int)>(orders.Count);
        foreach (var (bagId, itemId, count) in orders)
        {
            lines.Add((bagId, count));
            _bot.Telemetry.Record(TelemetryEventType.Sold, snap.SelfLevel, _data?.ItemName(itemId) ?? $"#{itemId}", count);
        }
        OnLog?.Invoke($"Selling {lines.Count} junk/common stack(s).");
        await _bot.ShopBuySellAsync(lines, ct);
    }

    private List<(int bagId, int itemId, int count)> BuildSellOrders(WorldState w)
    {
        var equipped = new HashSet<int>();
        foreach (var b in w.Self.EquippedBagIds)
            if (b != 0) equipped.Add(b);

        var orders = new List<(int, int, int)>();

        // Custom sell from MCP sell_item — only sell the requested item ids (qty <= 0 = sell every stack).
        if (_customSellItems != null)
        {
            foreach (var (itemId, requestedQty) in _customSellItems)
            {
                var remaining = requestedQty <= 0 ? int.MaxValue : requestedQty;
                foreach (var it in w.Self.Inventory)
                {
                    if (orders.Count >= 100) break;
                    if (it.ItemId != itemId) continue;
                    if (equipped.Contains(it.BagId)) continue; // never sell what we're wearing
                    var take = Math.Min(remaining, it.Count);
                    if (take <= 0) continue;
                    orders.Add((it.BagId, it.ItemId, take));
                    remaining -= take;
                    if (remaining <= 0) break;
                }
                if (orders.Count >= 100) break;
            }
            return orders;
        }

        // Operator-queued items (set via MCP queue_sell ahead of the trip) go FIRST, before policy junk.
        // Qty <= 0 = sell every stack of that id. Items already added by the queue are tracked so the
        // policy pass below doesn't duplicate them. Drain the queue here — once we've built sell orders
        // for these items, they're committed for this trip.
        var queuedBagIds = new HashSet<int>();
        List<(int itemId, int qty)>? queuedSnapshot = null;
        lock (_queuedSellLock)
        {
            if (_queuedSellItems.Count > 0)
            {
                queuedSnapshot = _queuedSellItems.ToList();
                _queuedSellItems.Clear();
            }
        }
        if (queuedSnapshot != null)
        {
            foreach (var (itemId, requestedQty) in queuedSnapshot)
            {
                var remaining = requestedQty <= 0 ? int.MaxValue : requestedQty;
                foreach (var it in w.Self.Inventory)
                {
                    if (orders.Count >= 100) break;
                    if (it.ItemId != itemId) continue;
                    if (equipped.Contains(it.BagId)) continue;
                    var take = Math.Min(remaining, it.Count);
                    if (take <= 0) continue;
                    orders.Add((it.BagId, it.ItemId, take));
                    queuedBagIds.Add(it.BagId);
                    remaining -= take;
                    if (remaining <= 0) break;
                }
                if (orders.Count >= 100) break;
            }
        }

        // Default: ShopPolicy decides what's junk worth selling.
        foreach (var it in w.Self.Inventory)
        {
            if (orders.Count >= 100) break; // stay well under the server's per-order line cap
            if (queuedBagIds.Contains(it.BagId)) continue; // already queued explicitly
            var def = _data?.Item(it.ItemId);
            if (ShopPolicy.ShouldSell(def, it, equipped.Contains(it.BagId), _config))
                orders.Add((it.BagId, it.ItemId, it.Count));
        }
        return orders;
    }

    private int FindNpcEntityId(WorldState w)
    {
        var best = 0;
        var bestD = int.MaxValue;
        foreach (var e in w.Entities.Values)
        {
            if (!e.IsNpc) continue;
            var d = Math.Max(Math.Abs(e.Position.X - _shopX), Math.Abs(e.Position.Y - _shopY));
            if (d <= 3 && d < bestD) { bestD = d; best = e.Id; }
        }
        return best;
    }

    /// <summary>The last A* path the travel walker planned (map, cells start..goal), for the dashboard's world view.
    /// Stale once the bot stops traveling, so readers should check the mode and map.</summary>
    public (string Map, IReadOnlyList<(int x, int y)> Cells)? PlannedPath { get; private set; }

    // Walk toward (tx,ty) along an A* path so the server gets a reachable, obstacle-aware target. The exact
    // tile may be blocked (NPCs stand on blocked tiles) and far/blocked WalkTo targets are rejected, so we
    // aim at a walkable cell near the goal and only send a waypoint `step` tiles ahead along the path.
    private async Task WalkPathTowardAsync(Snapshot snap, int tx, int ty, int step, CancellationToken ct)
    {
        var map = _data?.GetWalkMap(snap.Map);
        if (map == null) { await _bot.WalkToAsync(Math.Max(1, tx), Math.Max(1, ty), ct); return; }
        if (!map.TryFindWalkableNear(tx, ty, 6, out var gx, out var gy)) { gx = tx; gy = ty; }
        // Use portal-avoidance cost so the A* path bends around portals it isn't trying to reach. The
        // destination itself may be ON a portal cell (when we're walking to a forward portal) and that's
        // fine — A* still picks the destination because it's the goal; the path leading to it avoids
        // every OTHER portal.
        _walkGoal = (snap.Map, gx, gy, DateTime.UtcNow);
        var path = map.FindPath(snap.SelfPos.X, snap.SelfPos.Y, gx, gy, extraCost: MakePathCost(snap.Map));
        PlannedPath = path is { Count: > 1 } ? (snap.Map, path) : null;
        if (path == null || path.Count <= 1)
        {
            LogTravelOnce($"Travel: no path on {snap.Map} from ({snap.SelfPos.X},{snap.SelfPos.Y}) to ({gx},{gy}) — asking the server to walk there directly.");
            await _bot.WalkToAsync(Math.Max(1, gx), Math.Max(1, gy), ct); // no path; let the server try directly
            return;
        }
        LogTravelOnce($"Travel: walking to ({gx},{gy}) on {snap.Map}, {path.Count} cells.");
        // The server walks straight-line from current pos to whatever waypoint we send. If that line
        // crosses a portal cell, the bot warps mid-walk. Pick the furthest path index whose straight
        // line from us is portal-free — destination portal itself excluded since that's our actual goal.
        var maxStep = Math.Min(step, path.Count - 1);
        var safeStep = FurthestStraightLineSafeStep(snap.Map, snap.SelfPos.X, snap.SelfPos.Y, path, maxStep,
                                                     allowedPortalCell: (gx, gy));
        if (safeStep < 1) safeStep = 1; // always make at least one cell of progress
        var wp = path[safeStep];
        await _bot.WalkToAsync(Math.Max(1, wp.x), Math.Max(1, wp.y), ct);
    }

    /// <summary>Return the largest <c>i &lt;= maxStep</c> such that the server's straight-line walk from
    /// (fromX,fromY) to <c>path[i]</c> doesn't pass through (or step onto) any portal cell. Lets us issue
    /// a multi-cell WalkTo when safe, fall back to a 1-cell step when the next move is right next to a
    /// back-portal. <paramref name="allowedPortalCell"/> is the bot's intended GOAL portal (if any) —
    /// crossing that one is fine since it's where we want to go.</summary>
    private int FurthestStraightLineSafeStep(string map, int fromX, int fromY,
        System.Collections.Generic.IReadOnlyList<(int x, int y)> path, int maxStep,
        (int x, int y)? allowedPortalCell = null)
    {
        if (_data == null) return maxStep;
        for (var i = maxStep; i >= 1; i--)
        {
            var (tx, ty) = path[i];
            if (!WalkMayCrossPortal(map, fromX, fromY, tx, ty, allowedPortalCell))
                return i;
        }
        return 0;
    }

    /// <summary>True if the server's walk from (x0,y0) to (x1,y1) could step on a warp footprint other than the
    /// goal portal. The server runs its own pathfinder rather than a straight line (from alberta's (102,153) it
    /// went diagonal first, into the shop warp at 98,153, and the bots bounced in and out forever), so test the
    /// whole box between the two points: on open ground every shortest 8-way path stays inside it.</summary>
    private bool WalkMayCrossPortal(string map, int x0, int y0, int x1, int y1, (int x, int y)? allowedPortalCell)
    {
        if (_data == null || (x0 == x1 && y0 == y1)) return false;
        int minX = Math.Min(x0, x1), maxX = Math.Max(x0, x1), minY = Math.Min(y0, y1), maxY = Math.Max(y0, y1);
        foreach (var f in _data.World.PortalFootprintsOn(map))
        {
            if (f.maxX < minX || f.minX > maxX || f.maxY < minY || f.minY > maxY) continue;
            if (allowedPortalCell is { } a && a.x >= f.minX - 1 && a.x <= f.maxX + 1 && a.y >= f.minY - 1 && a.y <= f.maxY + 1)
                continue; // goal portal: crossing it is the point
            return true;
        }
        return false;
    }

    private string _lastReachabilityLog = "";
    private string _lastTravelLog = "";
    // ponytail: one global knob; raise it if bots still cross deadly maps, lower it if detours get silly.
    private const float MapDangerRouteWeight = 5f;
    private string? _lastRouteLog;

    private void LogTravelOnce(string msg)
    {
        var key = msg.Contains(" cells.") ? msg[..msg.LastIndexOf(',')] : msg; // don't relog as the cell count drops
        if (key == _lastTravelLog) return;
        _lastTravelLog = key;
        OnLog?.Invoke(msg);
    }
    // Trailing-maps memory: never re-enter a map we just left within the last few cross-map hops.
    // Anti-loop guard for the case where the bot lands on a map's sub-region with only a back-portal to
    // the previous map (the BFS thinks both A↔B are valid via that portal; without this we'd ping-pong).
    private readonly Queue<string> _recentMapTrail = new();
    private string _lastTrailMap = "";
    private const int RecentMapMemory = 4;

    // Source-portal SOFT penalty: portals that previously led the bot into a walled-off sub-region get a
    // penalty added to their pick-score. They're NOT excluded — if a penalized portal is the only one
    // available, it'll still be used (we'd rather try the same portal twice than have the bot give up
    // entirely). The penalty decays over time so a one-off bad entry doesn't permanently bias selection.
    // Per-bot scoped (instance field), so one bot's trap doesn't affect others.
    private readonly Dictionary<(string, int, int), (int count, DateTime expires)> _portalPenalty = new();
    private static readonly TimeSpan PenaltyTtl = TimeSpan.FromMinutes(2);
    private const int PenaltyPerTrap = 100;       // soft penalty added to the portal's distance score
    private const int MaxPenalty = 400;           // cap so a bot doesn't accumulate runaway penalties

    // Sub-region trap detection. Set when StepTowardMapAsync gives up; cleared on map change or successful
    // portal pick. Drives the escalation ladder: at 5s+ penalize the entry portal AND Fly Wing, at 15s+
    // Butterfly Wing. The 5s threshold (was 1s) reduces false positives from transient blockers (mob in
    // the way, momentary path congestion) and dovetails with the Fly Wing trigger.
    private DateTime? _subRegionTrapSince;
    // Last portal we WALKED ONTO that caused a map transition. Used so when the trap is detected on the
    // destination map, we know which source-side portal to soft-penalize.
    private (string sourceMap, int x, int y)? _lastEntryPortal;

    // Apron-departure state — the bot just warped to a new map. For a short window we force a step-away
    // walk before considering any portal, and heavily soft-penalize the back-portal so the cheapest-portal
    // scorer naturally picks something else once it's allowed to run. Both signals decay automatically.
    private RebuildSharedData.Data.Position _landingCell;
    private string _landingFromMap = "";
    private DateTime _apronExpiresAt;
    private bool _hasLeftApron = true;
    private const int ApronCells = 6;
    private static readonly TimeSpan ApronTtl = TimeSpan.FromSeconds(2.5);
    // (ApronBackPortalPenalty constant deleted — see comment above the deleted ApronBackPortalScore method.)

    /// <summary>Called from TickAsync's map-change branch — begin the apron-departure phase. With the
    /// WorldGraph now handling portal selection structurally (it knows landing regions, so it never
    /// picks a back-portal as the next hop unless that's genuinely the right route), all this needs to
    /// do is record the landing cell, start the short anti-rewarp window, and reset trap state. The
    /// previous map-flap detection, trap-timer backdating, and apron back-portal scoring were defensive
    /// scaffolding around the bouncing bug WorldGraph eliminates by construction.</summary>
    internal void BeginApronDeparture(RebuildSharedData.Data.Position landingCell, string arrivedFrom, string arrivedOn)
    {
        _landingCell = landingCell;
        _landingFromMap = arrivedFrom ?? "";
        _apronExpiresAt = DateTime.UtcNow + ApronTtl;
        _hasLeftApron = false;
        _subRegionTrapSince = null; // arrival on a new map = forward progress; reset trap timer

        // If we just successfully crossed a previously-penalized portal, the entry was useful after all —
        // clear the penalty so the next route doesn't carry false-alarm bias for 2 minutes.
        if (_lastEntryPortal is { } prev) ClearPortalPenalty(prev.sourceMap, prev.x, prev.y);
        _lastEntryPortal = null;
    }

    /// <summary>Refresh _hasLeftApron — distance-based primary, time-based safety cap. Called once per
    /// tick before any portal pick.</summary>
    private void UpdateApronGate(RebuildSharedData.Data.Position selfPos)
    {
        if (_hasLeftApron) return;
        var d = Math.Max(Math.Abs(selfPos.X - _landingCell.X), Math.Abs(selfPos.Y - _landingCell.Y));
        if (d >= ApronCells || DateTime.UtcNow > _apronExpiresAt + TimeSpan.FromSeconds(5))
            _hasLeftApron = true;
    }

    /// <summary>Step away from the portal we just landed on. Picks a walkable cell ~8 tiles from the
    /// landing cell, then runs our portal-aware A* so the PATH itself never crosses another portal cell
    /// (the server's pathfinder will route us directly through one otherwise, re-warping mid-departure).
    /// Caller short-circuits the rest of the travel pipeline this tick when this runs.</summary>
    private async Task StepAwayFromApronAsync(Snapshot snap, CancellationToken ct)
    {
        if (_data == null) { _hasLeftApron = true; return; }
        var walk = _data.GetWalkMap(snap.Map);
        var dirs = new (int dx, int dy)[]
        {
            ( 1, 0), (-1, 0), (0, 1), (0,-1), (1, 1), (1,-1), (-1, 1), (-1,-1)
        };
        for (var i = dirs.Length - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
        }
        const int Reach = 8;
        // Portal-avoiding cost overlay so the path bends around any portals near the route — this is
        // the fix for the prt_fild09 bounce-loop, where the raw WalkTo was server-routed through a
        // back-portal cell, re-warping mid-departure.
        var cost = MakePathCost(snap.Map);
        foreach (var (dx, dy) in dirs)
        {
            var tx = _landingCell.X + dx * Reach;
            var ty = _landingCell.Y + dy * Reach;
            if (tx <= 0 || ty <= 0) continue;
            if (walk == null)
            {
                await _bot.WalkToAsync(tx, ty, ct);
                return;
            }
            if (!walk.TryFindWalkableNear(tx, ty, 4, out var fx, out var fy)) continue;
            // Real warp-footprint check (handles fat warps like pay_fild01's 5×13 pay_fild07 portal that
            // IsNearPortal(center, 1) would have missed). Adding a 1-cell margin gives a small safety
            // band around the rectangle so we don't end up adjacent and step in next tick.
            if (_data.World.IsInPortalFootprint(snap.Map, fx, fy, margin: 1)) continue;
            // Use A* so the path itself bends away from portal cells.
            var path = walk.FindPath(snap.SelfPos.X, snap.SelfPos.Y, fx, fy, 6000, cost);
            if (path == null || path.Count < 2) continue;
            // The bot's A* avoids portal cells, but we only get to send ONE waypoint at a time and the
            // server's pathfinder ignores our cost overlay — it walks straight from current to waypoint.
            // So the waypoint must satisfy: the SERVER's straight-line walk from current to waypoint
            // crosses no portal. Pick the furthest path index that's still safe; cap at index 4 so we
            // don't issue a 30-cell hop in one packet.
            var maxStep = Math.Min(4, path.Count - 1);
            var safeStep = FurthestStraightLineSafeStep(snap.Map, snap.SelfPos.X, snap.SelfPos.Y, path, maxStep);
            if (safeStep < 1) continue; // even one cell forward crosses a portal — try the next direction
            var wp = path[safeStep];
            await _bot.WalkToAsync(Math.Max(1, wp.x), Math.Max(1, wp.y), ct);
            return;
        }
        _hasLeftApron = true;
    }

    private void RememberMap(string map)
    {
        if (string.IsNullOrEmpty(map)) return;
        if (string.Equals(map, _lastTrailMap, StringComparison.OrdinalIgnoreCase)) return;
        // If this map is already in our recent trail, we just flapped back — that's NOT forward
        // progress, so don't reset the trap timer. Otherwise the 5s Fly Wing escalation never fires
        // for a back-and-forth ping-pong (the timer resets every transition).
        var isFlap = _recentMapTrail.Contains(map);
        _lastTrailMap = map;
        _recentMapTrail.Enqueue(map);
        while (_recentMapTrail.Count > RecentMapMemory) _recentMapTrail.Dequeue();
        if (!isFlap) _subRegionTrapSince = null;
    }

    /// <summary>Read the current soft penalty for a source portal. Returns 0 when no penalty is active
    /// (cleared / expired). The penalty is ADDED to the portal's path-cost score in TryPickPortalTo so
    /// alternatives are preferred — but a penalized portal is never excluded outright.</summary>
    private int GetPortalPenalty(string map, int x, int y)
    {
        var key = (map, x, y);
        if (!_portalPenalty.TryGetValue(key, out var v)) return 0;
        if (DateTime.UtcNow >= v.expires) { _portalPenalty.Remove(key); return 0; }
        return v.count;
    }

    private void RegisterPortalPenalty(string map, int x, int y)
    {
        var key = (map, x, y);
        var prev = _portalPenalty.TryGetValue(key, out var v) ? v.count : 0;
        var next = Math.Min(MaxPenalty, prev + PenaltyPerTrap);
        _portalPenalty[key] = (next, DateTime.UtcNow + PenaltyTtl);
    }

    /// <summary>Successful traversal of a portal — clear any accumulated penalty so a bot that learns to
    /// route around a trap once doesn't carry the bias forever.</summary>
    private void ClearPortalPenalty(string map, int x, int y)
    {
        _portalPenalty.Remove((map, x, y));
    }

    // ApronBackPortalScore / ApronBackPortalPenalty deleted: with the region-aware WorldGraph, the
    // planner doesn't pick a back-portal unless that's genuinely the right route (because it knows the
    // destination region of every edge). The +999 score band-aid is no longer needed. If the WorldGraph
    // ever fails to load (no warp scripts on disk), the legacy fallback's hop-strict + soft-trail filter
    // is sufficient — and any remaining bouncing in that mode is a deployment problem to fix, not
    // something to defend against here.

    private async Task StepTowardMapAsync(Snapshot snap, string destMap, CancellationToken ct, (int X, int Y)? destCell = null)
    {
        if (_data == null) return;
        RememberMap(snap.Map); // record where we are before picking the next hop

        // Apron-departure: when we just arrived on this map (set by BotBehavior's map-change branch),
        // force a step away from the landing cell before considering portals — the cheapest portal here
        // is almost certainly the back-portal we came through. Decays automatically by distance or time.
        UpdateApronGate(snap.SelfPos);
        if (!_hasLeftApron && DateTime.UtcNow < _apronExpiresAt)
        {
            // Suppress trap detection during legitimate departure — we're not stuck, we're leaving.
            _subRegionTrapSince = null;
            await StepAwayFromApronAsync(snap, ct);
            return;
        }

        // Pick a portal we can actually reach from our current cell. The map may contain disconnected
        // walkable regions (e.g. a fenced courtyard with one entry) and the obvious portal could be in a
        // different region than the bot. If we can't reach any portal toward the next hop, look for
        // portals to ANY neighbouring map that strictly REDUCE hop-distance to the destination — never
        // backtrack to a map we just came from. Last resort: escalate via blacklist + Fly Wing.
        if (TryPickReachablePortalToward(snap, destMap, out var px, out var py, out var via, out var kafraInfo, destCell))
        {
            _lastReachabilityLog = "";
            _subRegionTrapSince = null;
            // Kafra teleport: drive the NPC dialog instead of walking a portal cell. The kafra sub-FSM
            // handles walk-to-NPC + click + menu selection + warp wait; on the destination map the
            // normal apron-departure flow takes over via the map-change event.
            if (kafraInfo != null)
            {
                _lastEntryPortal = null; // kafra warp isn't a portal-crossing — no entry portal to blame
                await StepKafraInteractionAsync(snap, px, py, kafraInfo, ct);
                return;
            }
            // Portal hop — reset kafra sub-FSM in case we just switched off it.
            ResetKafraStep();
            // Remember this entry so a future trap on the destination map can blacklist this portal.
            _lastEntryPortal = (snap.Map, px, py);
            var dist = Math.Max(Math.Abs(px - snap.SelfPos.X), Math.Abs(py - snap.SelfPos.Y));
            if (dist <= 4) { await _bot.WalkToAsync(Math.Max(1, px), Math.Max(1, py), ct); return; }
            if (await TryTravelFlyWingAsync(snap, px, py, dist, ct)) return;
            await WalkPathTowardAsync(snap, px, py, 16, ct);
            return;
        }
        ResetKafraStep(); // no portal found — clear any leftover kafra state

        // No reachable portal — sub-region trap. Track when we first detected this and escalate over time:
        //   * 1s in: blacklist the source-side portal we used to enter (so the next attempt picks a
        //     different one).
        //   * 5s in: Fly Wing if available (teleports within the same map, usually to a different
        //     walkable region — often the one with the forward portal).
        //   * 15s in: Butterfly Wing as last resort (warps to save point; loses progress but unbricks).
        _subRegionTrapSince ??= DateTime.UtcNow;
        var trapSecs = (DateTime.UtcNow - _subRegionTrapSince.Value).TotalSeconds;

        var key = $"unreachable:{snap.Map}->{destMap}";
        if (_lastReachabilityLog != key)
        {
            _lastReachabilityLog = key;
            OnLog?.Invoke($"Travel: no reachable portal from ({snap.SelfPos.X},{snap.SelfPos.Y}) on {snap.Map} toward {destMap}{(via != null ? $" (via {via})" : "")}. Sub-region trap?");
        }

        // Step 1 — soft-penalize the entry portal so the NEXT pick from that source map biases toward
        // a different portal IF one exists. Threshold is 5s (not 1s) — a brief blocker shouldn't trigger
        // this. The penalty decays over PenaltyTtl and is cleared on a successful traversal, and is a
        // SCORE bias (not an exclusion) so a one-portal route is never made unreachable.
        if (trapSecs >= 5.0 && _lastEntryPortal is { } entry && GetPortalPenalty(entry.sourceMap, entry.x, entry.y) == 0)
        {
            RegisterPortalPenalty(entry.sourceMap, entry.x, entry.y);
            OnLog?.Invoke($"Sub-region trap: soft-penalizing source portal {entry.sourceMap}({entry.x},{entry.y}) — alternatives will be preferred for the next ~{PenaltyTtl.TotalMinutes:F0} min.");
        }

        // Step 2/3 — wing escape. We override AutoEscapeWhenStuck for this case because the trap is
        // unrecoverable by any other means — the bot can't walk anywhere useful.
        if (trapSecs >= 5.0)
        {
            await TryTrapWingEscapeAsync(trapSecs, ct);
        }
    }

    // Fly Wing shortcut on long walks: fly only when we're clearly farther from the portal than a random landing
    // would be on average. At most 5 per map visited, and a few stay in the bag for escapes.
    private const int TravelFlyWingDistance = 100, TravelFlyWingsPerMap = 5, TravelFlyWingReserve = 5; // reserve = ambush escapes
    private const double TravelFlyWingMargin = 1.3;
    private readonly Dictionary<(string, int, int), double> _averageLandingDistance = new();
    private string? _travelWingMap;
    private int _travelWingsUsed;
    private DateTime _nextTravelWing;

    private async Task<bool> TryTravelFlyWingAsync(Snapshot snap, int portalX, int portalY, int distanceToPortal, CancellationToken ct)
    {
        if (distanceToPortal < TravelFlyWingDistance || DateTime.UtcNow < _nextTravelWing || _data?.IsUsableItem(_config.FlyWingItemId) != true)
            return false;
        var average = AverageLandingDistance(snap.Map, portalX, portalY);
        if (average is not { } expected || distanceToPortal < expected * TravelFlyWingMargin) return false;
        if (_travelWingMap == null) { _travelWingMap = snap.Map; _travelWingsUsed = 0; }
        if (_travelWingsUsed >= TravelFlyWingsPerMap || CountInInventory(_config.FlyWingItemId) <= TravelFlyWingReserve) return false;
        _travelWingsUsed++;
        _nextTravelWing = DateTime.UtcNow.AddSeconds(2);
        OnLog?.Invoke($"Travel: Fly Wing shortcut on {snap.Map}, {distanceToPortal} cells from the portal vs ~{expected:0} from a random landing ({_travelWingsUsed}/{TravelFlyWingsPerMap} on this map).");
        await _bot.UseInventoryItemAsync(_config.FlyWingItemId, -1, ct);
        return true;
    }

    // Mean straight-line distance from a random walkable cell to the portal: where a Fly Wing lands on average.
    // ponytail: straight-line, not walked distance; walls make real walks longer on mazy maps.
    private double? AverageLandingDistance(string map, int portalX, int portalY)
    {
        if (_averageLandingDistance.TryGetValue((map, portalX, portalY), out var cached)) return cached;
        if (_data?.GetWalkMap(map) is not { } walk) return null;
        var random = new Random(portalX * 31 + portalY);
        double total = 0;
        var samples = 0;
        for (var tries = 0; tries < 5000 && samples < 400; tries++)
        {
            int x = random.Next(walk.Width), y = random.Next(walk.Height);
            if (!walk.IsWalkable(x, y)) continue;
            total += Math.Max(Math.Abs(x - portalX), Math.Abs(y - portalY));
            samples++;
        }
        double? average = samples > 0 ? total / samples : null;
        if (average is { } value) _averageLandingDistance[(map, portalX, portalY)] = value;
        return average;
    }

    /// <summary>Wing-escape regardless of <see cref="BotBehaviorConfig.AutoEscapeWhenStuck"/> — the
    /// trap branch knows the bot has zero forward path and Fly Wing is the only recovery short of a
    /// server restart. Throttled so we don't fire one every tick.</summary>
    private DateTime _nextWingEscape = DateTime.MinValue;
    private async Task TryTrapWingEscapeAsync(double trapSecs, CancellationToken ct)
    {
        if (_data == null) return;
        if (DateTime.UtcNow < _nextWingEscape) return;
        // Find what we have. Fly Wing first; Butterfly Wing only after the longer threshold so we don't
        // throw the bot back to save point on a 5-second trap that Fly Wing alone could solve.
        var (hasFly, hasButterfly) = _bot.WithState(w =>
        {
            var fly = false; var b = false;
            foreach (var it in w.Self.Inventory)
            {
                if (it.ItemId == _config.FlyWingItemId) fly = true;
                else if (it.ItemId == _config.ButterflyWingItemId) b = true;
            }
            return (fly, b);
        });

        var pick = 0;
        if (hasFly && _data.IsUsableItem(_config.FlyWingItemId))
            pick = _config.FlyWingItemId;
        else if (trapSecs >= 15.0 && hasButterfly && _data.IsUsableItem(_config.ButterflyWingItemId))
            pick = _config.ButterflyWingItemId;
        if (pick == 0) return;

        _nextWingEscape = DateTime.UtcNow.AddSeconds(3);
        OnLog?.Invoke($"Sub-region trap escape: using {(pick == _config.FlyWingItemId ? "Fly Wing" : "Butterfly Wing")} (trapped {trapSecs:F0}s).");
        await _bot.UseInventoryItemAsync(pick, -1, ct);
    }

    /// <summary>True if we can find a walkable portal on the current map that leads (eventually) toward
    /// <paramref name="destMap"/>. Prefers the BFS-computed next-hop, but if every portal to that next-hop
    /// is in a different walkable region than the bot, we fall back to any portal we CAN reach whose
    /// destination has a route to <paramref name="destMap"/>. Returns the chosen portal coords and the
    /// first-hop map name (for logging).</summary>
    /// <summary>The Real Cross-Map Pathfinder. Uses the WorldGraph's Dijkstra over (mapCode, regionId)
    /// nodes — every portal knows its destination cell, so a route is only proposed when the bot's
    /// current region IS actually connected to a portal whose destination region IS connected to the
    /// destination map. Sub-region traps stop happening because they're encoded into the graph: the bad
    /// region simply doesn't have an outgoing edge to the next map, and Dijkstra picks a different
    /// portal that does. Falls back to the legacy BFS-based picker only if the world graph isn't built
    /// (no warp scripts on disk).</summary>
    private bool TryPickReachablePortalTowardGraph(Snapshot snap, string destMap, out int px, out int py,
        out string? viaMap, out WorldGraph.KafraEdgeInfo? kafra, (int X, int Y)? destCell = null)
    {
        px = py = 0;
        viaMap = null;
        kafra = null;
        if (_data == null || !_data.World.IsBuilt) return false;
        // We don't know a specific destination cell on destMap — pick any walkable seed cell. The
        // planner's region-resolution will pick that cell's region as the goal node; any portal that
        // lands the bot in destMap region X works as "arrived" if X equals the goal region. To accept
        // ANY region of destMap, target the cell where the LARGEST region of destMap typically lives —
        // good enough for now is to find the first walkable cell.
        // Aim at a portal landing cell when one exists: the map's center can sit in a pocket no portal
        // reaches (pay_fild04), which made Plan() return null and stranded the bot on its way there.
        // A known target cell (a shop NPC) wins: indoor maps like prt_in are many rooms, and only the
        // right door reaches the right one.
        var destSeed = destCell is { } dc ? new RebuildSharedData.Data.Position(dc.X, dc.Y)
            : _data.World.EntryCell(destMap) is { } entry
            ? new RebuildSharedData.Data.Position(entry.X, entry.Y)
            : FindAnyWalkableCell(destMap);
        if (destSeed == null) return false;
        // Edge filter: drop kafra edges whose NPC we've blacklisted (a previous interaction timed out).
        // Lets Dijkstra pick portal-walking or a different kafra without rebuilding the graph.
        // ponytail: checks each teleport against current zeny, not the route's total; fine while routes use one kafra hop.
        var plan = PlanRoute(snap.Map, snap.SelfPos.X, snap.SelfPos.Y, destMap, destSeed.Value.X, destSeed.Value.Y);
        if (plan == null || plan.Edges.Count == 0) return false;
        var route = $"Route to {destMap}: {snap.Map} → {string.Join(" → ", plan.Edges.Select(e => e.Kafra != null ? $"{e.DstMap} (kafra {e.Kafra.Zeny}z)" : e.DstMap))}"
                    + $" (danger {string.Join(", ", plan.Edges.Select(e => GetMapDanger(e.DstMap).ToString("0")))}).";
        if (route != _lastRouteLog) { _lastRouteLog = route; OnLog?.Invoke(route); }
        var first = plan.Edges[0];
        px = first.Sx;
        py = first.Sy;
        viaMap = first.DstMap;
        kafra = first.Kafra; // non-null when the next hop is a kafra teleport (drives a dialog instead of crossing a portal)
        return true;
    }

    // Shop choice and travel must agree on which teleports exist: the shop picker used to price a Kafra hop the bot
    // couldn't pay for, then travel walked six maps instead.
    private bool UsableEdge(WorldGraph.Edge e, int zeny) =>
        e.Kafra == null || (!IsKafraBlacklisted(e.SrcMap, e.Sx, e.Sy) && e.Kafra.Zeny <= zeny);

    // Every route the bot plans or prices. Maps with an aggressive monster it loses to are walls on the way (not just
    // expensive): with only the danger penalty, Bot1 on yuno_fild04 found every map "through yuno_fild03", the shortest
    // way, though the Yuno Kafra avoided it. Falls back to the risky route when nothing else connects.
    private WorldGraph.TravelPlan? PlanRoute(string fromMap, int fromX, int fromY, string toMap, int toX, int toY)
    {
        var zeny = _bot.WithState(w => w.Self.Zeny);
        var estimates = _mapEstimates;
        bool Unsafe(string map) => !string.Equals(map, fromMap, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(map, toMap, StringComparison.OrdinalIgnoreCase)
            && estimates.TryGetValue(map, out var estimate) && !estimate.Safe;
        float Penalty(string map) => GetMapDanger(map) * MapDangerRouteWeight;
        return _data!.World.Plan(fromMap, fromX, fromY, toMap, toX, toY, edgeFilter: e => UsableEdge(e, zeny) && !Unsafe(e.DstMap), mapPenalty: Penalty)
            ?? _data.World.Plan(fromMap, fromX, fromY, toMap, toX, toY, edgeFilter: e => UsableEdge(e, zeny), mapPenalty: Penalty);
    }

    // Fly and Butterfly Wings save time but never decide progress: they get a quarter of the zeny left after the Kafra
    // fares home, so a poor bot keeps its money for gear and teleports instead of draining it on wings.
    private const double ConvenienceBudgetShare = 0.25;
    private bool IsConvenienceItem(int itemId) => itemId > 0 && (itemId == _config.FlyWingItemId || itemId == _config.ButterflyWingItemId);
    private static int ConvenienceBudget(int zeny, int homeFares) => (int)(Math.Max(0, zeny - homeFares) * ConvenienceBudgetShare);

    // Zeny-cost skills (Mammonite) get their own pool, the same share as the wings: filled from the purse on the first
    // cast after each shop trip, spent per cast, so a merchant can't pour its whole purse into damage.
    private int _skillZenyLeft = -1; // -1 = refill on the next cast
    private int SkillZenyCost(CharacterSkill skill, int level)
    {
        if (skill != CharacterSkill.Mammonite) return 0;
        var cost = 50 * level; // server: MammoniteHandler.GetMammoniteCost
        var discount = KnowsSkill(CharacterSkill.Discount, out var discountLevel) && discountLevel > 0 ? 5 + discountLevel * 2 : 0;
        return cost - cost * discount / 100;
    }

    // Kafra fares on the planned way home from here. Restocking leaves them in the purse: Bot1 and Bot5 spent their
    // last zeny on Fly Wings in izlude_in, and izlude has no walking exit, so they were stranded.
    private int ZenyForTripHome(Snapshot snap)
    {
        if (_data == null || string.IsNullOrEmpty(_config.HomeMap) || _data.World.EntryCell(_config.HomeMap) is not { } entry) return 0;
        var plan = PlanRoute(snap.Map, snap.SelfPos.X, snap.SelfPos.Y, _config.HomeMap, entry.X, entry.Y);
        return plan?.Edges.Sum(e => e.Kafra?.Zeny ?? 0) ?? 0;
    }

    private RebuildSharedData.Data.Position? FindAnyWalkableCell(string map)
    {
        if (_data == null) return null;
        var walk = _data.GetWalkMap(map);
        if (walk == null) return new RebuildSharedData.Data.Position(1, 1);
        // Try the geometric centre first — usually walkable in towns / fields.
        if (walk.TryFindWalkableNear(walk.Width / 2, walk.Height / 2, 50, out var cx, out var cy))
            return new RebuildSharedData.Data.Position(cx, cy);
        // Otherwise scan a coarse grid; cheap, runs once per Plan() call.
        for (var y = 4; y < walk.Height; y += 8)
        for (var x = 4; x < walk.Width; x += 8)
            if (walk.IsWalkable(x, y)) return new RebuildSharedData.Data.Position(x, y);
        return null;
    }

    private bool TryPickReachablePortalToward(Snapshot snap, string destMap, out int px, out int py,
        out string? viaMap, out WorldGraph.KafraEdgeInfo? kafra, (int X, int Y)? destCell = null)
    {
        px = py = 0;
        viaMap = null;
        kafra = null;
        if (_data == null) return false;

        // FIRST CHOICE: the region-aware world graph. When built, it knows BOTH endpoints of every
        // portal AND the connected-components decomposition of every map. A returned plan is by
        // construction free of sub-region traps — every portal in the chain lands the bot in a region
        // that has a path forward. Kafra warps are included as additional edges; the first edge may
        // therefore be a kafra-teleport hop instead of a portal-walk hop.
        if (TryPickReachablePortalTowardGraph(snap, destMap, out px, out py, out viaMap, out kafra, destCell))
            return true;

        // When the WorldGraph IS built but returns no plan, the bot's current REGION genuinely has no
        // forward path to destMap. The legacy BFS below would happily pick a portal whose destination
        // is "nearer" in map-graph terms but which lands the bot in a dead-end sub-region — exactly the
        // moc_fild02↔moc_fild03 flap loop. Trust the graph: if it says no path, fall through to the
        // trap branch in StepTowardMapAsync (which will Fly Wing the bot to a different region).
        if (_data.World.IsBuilt) return false;

        // Legacy BFS fallback — only used when the WorldGraph isn't loaded (no warp scripts on disk).
        var hop = _data.NextHopToward(snap.Map, destMap);
        if (hop == null) return false;

        var walkmap = _data.GetWalkMap(snap.Map);

        // First try: portals on snap.Map going to the BFS-preferred next hop, ranked by A*-reachable
        // distance from the bot's cell.
        if (TryPickPortalTo(snap, hop, walkmap, out px, out py))
        {
            viaMap = hop;
            return true;
        }

        // Fallback: scan all portals on the current map. CRITICAL constraints to avoid a sub-region
        // ping-pong:
        //   (a) the portal's destination must be STRICTLY CLOSER to destMap than where we are now
        //       (hops(portalDest, destMap) < hops(currentMap, destMap)) — otherwise we'd take a step
        //       that doesn't reduce graph distance, and the next tick on the new map would just send us
        //       back through the same portal.
        //   (b) the portal must not lead into a map we visited in the last RecentMapMemory hops —
        //       protects against (a) being satisfied via a longer detour that still cycles.
        var currentHops = _data.HopCount(snap.Map, destMap);
        if (currentHops < 0) return false; // current map has no route to destMap at all — give up
        var bestScore = int.MaxValue;
        foreach (var p in _data.PortalsOn(snap.Map))
        {
            if (string.Equals(p.To, hop, StringComparison.OrdinalIgnoreCase)) continue; // already tried via the BFS hop
            var hops = _data.HopCount(p.To, destMap);
            if (hops < 0) continue;                       // dead-end map
            if (hops >= currentHops) continue;            // (a) doesn't bring us closer — would loop
            // (b) Soft trail bias — recent maps are deprioritized BUT not excluded, so a legitimate
            // back-route (we walked through B but the real path home goes A → B → home) is still chosen
            // when it's the only option. 200 is enough to defer to alternatives, far less than 999 which
            // would be effectively excluded.
            var trailPenalty = _recentMapTrail.Contains(p.To) ? 200 : 0;
            // Chain-danger: sum the per-map danger of every map between this portal and the final dest.
            // Routing a level-15 bot through moc_fild04 adds ~200; routing around via prt_fild05 adds ~0.
            // MapRiskWeight 2.0 means a 100-danger map detour costs ~200 score — equivalent to 200 walked
            // cells or 4 extra hops. Tuned so the bot still takes the shortest route when the long way
            // around is also dangerous.
            var chainDanger = _data.ChainDanger(p.To, destMap, GetMapDanger);
            if (chainDanger < 0) chainDanger = 0;
            var dangerPenalty = (int)(chainDanger * 2.0f);
            var penalty = GetPortalPenalty(snap.Map, p.X, p.Y) + trailPenalty + dangerPenalty;
            if (walkmap != null)
            {
                if (!walkmap.TryFindWalkableNear(p.X, p.Y, 4, out var gx, out var gy)) continue;
                var path = walkmap.FindPath(snap.SelfPos.X, snap.SelfPos.Y, gx, gy, 6000, MakeHazardOnlyCost());
                if (path == null) continue;
                var walkCost = path.Count;
                var score = walkCost + hops * 50 + penalty; // soft penalty defers to alternatives but doesn't exclude
                if (score < bestScore) { bestScore = score; px = p.X; py = p.Y; viaMap = p.To; }
            }
            else
            {
                // No walkmap: trust the warp graph, prefer fewer hops.
                var score = hops * 50 + Math.Abs(p.X - snap.SelfPos.X) + Math.Abs(p.Y - snap.SelfPos.Y) + penalty;
                if (score < bestScore) { bestScore = score; px = p.X; py = p.Y; viaMap = p.To; }
            }
        }
        return bestScore != int.MaxValue;
    }

    private bool TryPickPortalTo(Snapshot snap, string targetMap, WalkMap? walkmap, out int px, out int py)
    {
        px = py = 0;
        var candidates = new List<PortalEntry>();
        foreach (var p in _data!.PortalsOn(snap.Map))
            if (string.Equals(p.To, targetMap, StringComparison.OrdinalIgnoreCase))
                candidates.Add(p);
        if (candidates.Count == 0) return false;
        if (walkmap == null) { px = candidates[0].X; py = candidates[0].Y; return true; }

        // Sort by Manhattan distance PLUS any soft penalty (trap history + apron back-portal during the
        // post-landing window) — so the just-came-from portal and known-bad portals go to the back of
        // the queue but are still considered. If it's the only reachable option, it gets picked.
        candidates.Sort((a, b) =>
        {
            var sa = Math.Abs(a.X - snap.SelfPos.X) + Math.Abs(a.Y - snap.SelfPos.Y)
                     + GetPortalPenalty(snap.Map, a.X, a.Y);
            var sb = Math.Abs(b.X - snap.SelfPos.X) + Math.Abs(b.Y - snap.SelfPos.Y)
                     + GetPortalPenalty(snap.Map, b.X, b.Y);
            return sa.CompareTo(sb);
        });
        foreach (var p in candidates)
        {
            if (!walkmap.TryFindWalkableNear(p.X, p.Y, 4, out var gx, out var gy)) continue;
            var path = walkmap.FindPath(snap.SelfPos.X, snap.SelfPos.Y, gx, gy, 6000);
            if (path != null) { px = p.X; py = p.Y; return true; }
        }
        return false;
    }

    private static int ChooseOption(IReadOnlyList<string> options, ShopGoal goal)
    {
        var wanted = goal == ShopGoal.Buy
            ? new[] { "purchas", "buy", "shop", "trade" }
            : new[] { "sell" };
        for (var i = 0; i < options.Count; i++)
        {
            var o = options[i];
            if (string.IsNullOrWhiteSpace(o)) continue;
            var lower = o.ToLowerInvariant();
            foreach (var kw in wanted)
                if (lower.Contains(kw)) return i;
        }
        return -1;
    }

    private static int CancelOption(IReadOnlyList<string> options)
    {
        string[] cancels = { "cancel", "leave", "no thank", "bye", "end", "quit", "exit", "nothing", "stop", "goodbye" };
        for (var i = 0; i < options.Count; i++)
        {
            var lower = options[i].ToLowerInvariant();
            foreach (var kw in cancels)
                if (lower.Contains(kw)) return i;
        }
        return Math.Max(0, options.Count - 1); // last option is usually the way out
    }

    private async Task AbortShopAsync(string reason, CancellationToken ct)
    {
        OnLog?.Invoke($"AutoShop aborted: {reason}.");
        await _bot.CloseNpcConversationAsync(ct);
        EndShopTrip();
    }

    private void EndShopTrip()
    {
        _shopPhase = ShopPhase.None;
        _shopGoals.Clear();
        _shopOrderSent = false;
        _customBuyItems = null;
        _customSellItems = null;
        _shopCooldownUntil = DateTime.UtcNow.AddSeconds(60);
        OnLog?.Invoke("AutoShop: trip complete — resuming hunt.");
    }
}
