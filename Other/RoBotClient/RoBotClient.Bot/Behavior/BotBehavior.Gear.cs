using RebuildSharedData.Enum;
using RoBotClient.Bot.GameData;

namespace RoBotClient.Bot.Behavior;

// Auto-gear: wear the best thing we own in each slot, and when an NPC vendor sells something better that
// we can afford, buy it (one item per shop trip). "Best" = highest attack/defense the job may wear; for
// weapons the job's preferred class wins first (an Archer wants a bow, not a stronger knife). A bow only
// counts while we carry arrows — the server refuses to attack with an empty quiver.
// Deliberately ignores monster drops as a source: gear comes from what we already have or a vendor.
public sealed partial class BotBehavior
{
    private const int GearZenyReserve = 3000; // ponytail: flat reserve for potions + a Kafra hop; scale with level if bots go broke
    private const int ArrowStock = 500, ArrowLow = 100;

    private static readonly Dictionary<string, string[]> PreferredWeapons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Swordsman"] = new[] { "Sword", "2HSword" },
        ["Archer"] = new[] { "Bow" },
        ["Thief"] = new[] { "Dagger" },
        ["Mage"] = new[] { "Rod", "2HRod" },
        ["Acolyte"] = new[] { "Mace", "Rod" },
        // Novice / Merchant: no preference, strongest weapon they can hold.
    };

    // The inventory streams in over the first seconds after login; deciding before it settles buys
    // "upgrades" for gear we already own.
    private DateTime _nextGearCheck = DateTime.UtcNow.AddSeconds(10), _nextGearBuy = DateTime.MinValue;
    private readonly Dictionary<int, DateTime> _gearBuyRetryAt = new();

    private static EquipSlot? SlotOf(GameDatabase.GearInfo g) => (g.Type, g.Position) switch
    {
        (_, "MainHand" or "BothHands") => EquipSlot.RightHand,
        ("Armor", _) => EquipSlot.Body,
        ("Shield", _) => EquipSlot.LeftHand,
        ("Garment", _) => EquipSlot.Garment,
        ("Boots", _) => EquipSlot.Footgear,
        ("Headgear", "Top") => EquipSlot.HeadTop,
        ("Headgear", "Mid") => EquipSlot.HeadMid,
        ("Headgear", "Bottom") => EquipSlot.HeadBottom,
        _ => null, // ponytail: accessories and multi-slot headgear skipped; add once the simple slots pay off
    };

    private async Task TickGearAsync(Snapshot snap, CancellationToken ct)
    {
        if (_data == null || _data.Gear.Count == 0 || DateTime.UtcNow < _nextGearCheck) return;
        _nextGearCheck = DateTime.UtcNow.AddSeconds(3);

        var job = snap.SelfJobId == 0 ? "Novice" : JobName(snap.SelfJobId);
        PreferredWeapons.TryGetValue(job, out var preferred);
        var (inv, equipped, ammoId, zeny) = _bot.WithState(w => (
            w.Self.Inventory.Select(i => (i.BagId, i.ItemId, i.Count, Refine: (int)i.Refine)).ToList(),
            w.Self.EquippedBagIds.ToArray(), w.Self.AmmoId, w.Self.Zeny));
        var arrows = inv.Where(i => _data.Gear.TryGetValue(i.ItemId, out var g) && g.Type == "Arrow").Sum(i => i.Count);

        // Higher is better; null = can't or shouldn't wear it.
        int? Score(GameDatabase.GearInfo g, int refine)
        {
            if (g.MinLvl > snap.SelfLevel || !_data.CanEquip(g, job)) return null;
            if (g.Type == "Bow" && arrows == 0) return null;
            var pref = preferred != null && SlotOf(g) == EquipSlot.RightHand && preferred.Contains(g.Type) ? 100_000 : 0;
            return pref + g.Power + refine;
        }

        // 1. Arrows: keep the strongest ones in the quiver.
        var bestArrow = inv.Where(i => _data.Gear.TryGetValue(i.ItemId, out var g) && g.Type == "Arrow")
            .OrderByDescending(i => _data.Gear[i.ItemId].Power).Select(i => (int?)i.BagId).FirstOrDefault();
        if (bestArrow is { } arrowBag && inv.First(i => i.BagId == arrowBag).ItemId != ammoId)
        {
            OnLog?.Invoke($"Gear: loading {_data.ItemName(inv.First(i => i.BagId == arrowBag).ItemId)}.");
            await _bot.EquipItemAsync(arrowBag, ct);
            return;
        }

        // 2. Wear the best owned item per slot.
        var twoHanded = equipped[(int)EquipSlot.RightHand] is var wb and not 0
                        && inv.FirstOrDefault(i => i.BagId == wb).ItemId is var wid
                        && _data.Gear.TryGetValue(wid, out var wg) && wg.Position == "BothHands";
        var bestOwned = new Dictionary<EquipSlot, int>(); // slot -> best owned score (worn or not)
        foreach (var it in inv)
        {
            if (!_data.Gear.TryGetValue(it.ItemId, out var g) || SlotOf(g) is not { } slot) continue;
            if (Score(g, it.Refine) is not { } s) continue;
            bestOwned[slot] = Math.Max(bestOwned.GetValueOrDefault(slot, int.MinValue), s);
        }
        foreach (var (slot, best) in bestOwned)
        {
            if (slot == EquipSlot.LeftHand && twoHanded) continue;
            var worn = equipped[(int)slot];
            var wornItem = inv.FirstOrDefault(i => i.BagId == worn);
            var wornScore = worn != 0 && _data.Gear.TryGetValue(wornItem.ItemId, out var wg2) ? Score(wg2, wornItem.Refine) ?? int.MinValue : int.MinValue;
            if (wornScore >= best) continue;
            var pick = inv.First(i => _data.Gear.TryGetValue(i.ItemId, out var g) && SlotOf(g) == slot && Score(g, i.Refine) == best);
            OnLog?.Invoke($"Gear: equipping {_data.ItemDisplayName(pick.ItemId, (byte)pick.Refine)} ({slot}).");
            await _bot.EquipItemAsync(pick.BagId, ct);
            return;
        }
        // A bow with no arrows can't attack and nothing better was owned: bare hands at least hit.
        if (equipped[(int)EquipSlot.RightHand] is var rb and not 0 && arrows == 0
            && _data.Gear.TryGetValue(inv.FirstOrDefault(i => i.BagId == rb).ItemId, out var rg) && rg.Type == "Bow")
        {
            OnLog?.Invoke("Gear: out of arrows, unequipping the bow.");
            await _bot.UnequipItemAsync(rb, ct);
            return;
        }

        // 3. Shopping for upgrades: one item per trip, only with zeny to spare.
        var isSquadFollower = !string.IsNullOrEmpty(_config.SquadId) && !_config.IsSquadLeader;
        if (!_config.AutoShop || isSquadFollower || ShopTripActive || _forceShop || DateTime.UtcNow < _nextGearBuy) return;
        _nextGearBuy = DateTime.UtcNow.AddSeconds(30);
        if (TryBuyJobWeapon(job, inv.Select(i => i.ItemId), arrows, zeny, snap.SelfLevel)) return;
        var budget = zeny - GearZenyReserve;

        if (preferred?.Contains("Bow") == true && arrows < ArrowLow && budget > 0)
        {
            SetRestockTarget(1750, ArrowStock); // later natural shop trips keep the quiver full
            if (TryBuyGear(1750, Math.Min(ArrowStock - arrows, budget), "bow needs arrows")) return;
        }

        var upgrade = _data.Gear.Values
            .Where(g => SlotOf(g) is { } s && !(s == EquipSlot.LeftHand && twoHanded))
            .Select(g => (g, item: _data.Item(g.Id), slot: SlotOf(g)!.Value, score: Score(g, 0))) // bows wait until arrows arrive
            .Where(c => c.item?.SoldBy is { Count: > 0 } && c.item.Price <= budget && c.score is { } sc
                        && sc > bestOwned.GetValueOrDefault(c.slot, int.MinValue)
                        && c.g.Power > 0 // cosmetic gear (Flu Mask, def 0) is never worth a trip
                        && _gearBuyRetryAt.GetValueOrDefault(c.g.Id) < DateTime.UtcNow)
            .OrderBy(c => c.slot != EquipSlot.RightHand) // weapon first, then armor
            .ThenByDescending(c => c.score)
            .FirstOrDefault();
        if (upgrade.item != null)
            TryBuyGear(upgrade.g.Id, 1, $"{SlotLabel(upgrade.slot)} upgrade, {(upgrade.slot == EquipSlot.RightHand ? "atk" : "def")} {upgrade.g.Power}, {upgrade.item.Price}z");
    }

    // The bare minimum before any other gear: a weapon of the job's own kind. An Archer needs a bow (and arrows, or the bow
    // can't attack); every other job just needs some weapon. Bot5 hunted with a Knife because a bow only counts once arrows
    // are carried (so it never looked like an upgrade) and gear buys keep a 3000z reserve.
    private static readonly Dictionary<string, string> RequiredWeaponType = new(StringComparer.OrdinalIgnoreCase) { ["Archer"] = "Bow" };
    private const int JobWeaponZenyReserve = 500; // potions; the weapon itself matters more than the usual reserve
    private const int ArrowItemId = 1750;
    private DateTime _nextJobWeaponWarning = DateTime.MinValue;

    private bool TryBuyJobWeapon(string job, IEnumerable<int> ownedItemIds, int arrows, int zeny, int level)
    {
        RequiredWeaponType.TryGetValue(job, out var requiredType);
        bool Fits(GameDatabase.GearInfo g) => SlotOf(g) == EquipSlot.RightHand && g.MinLvl <= level && _data!.CanEquip(g, job)
                                             && (requiredType == null || g.Type == requiredType);
        var owned = ownedItemIds.Where(id => _data!.Gear.TryGetValue(id, out var g) && Fits(g)).Select(id => _data!.Gear[id]).ToList();
        var needsArrows = requiredType == "Bow" || owned.Any(g => g.Type == "Bow");
        var arrowPrice = Math.Max(1, _data!.Item(ArrowItemId)?.Price ?? 1);
        var spendable = zeny - JobWeaponZenyReserve;

        if (owned.Count > 0)
        {
            if (!needsArrows || arrows >= ArrowLow || spendable < arrowPrice * 10) return false;
            SetRestockTarget(ArrowItemId, ArrowStock);
            return TryBuyGear(new[] { (ArrowItemId, Math.Min(ArrowStock - arrows, spendable / arrowPrice)) }, $"the {job}'s bow needs arrows");
        }

        // None owned: the strongest one a vendor sells that we can afford, plus arrows for a bow, in one trip.
        var arrowCost = needsArrows ? Math.Min(ArrowStock, Math.Max(0, ArrowLow - arrows)) * arrowPrice : 0;
        var pick = _data.Gear.Values
            .Where(g => Fits(g) && _data.Item(g.Id) is { SoldBy.Count: > 0 } item && item.Price + arrowCost <= spendable
                        && _gearBuyRetryAt.GetValueOrDefault(g.Id) < DateTime.UtcNow)
            .OrderByDescending(g => g.Power).FirstOrDefault();
        if (pick == null)
        {
            if (DateTime.UtcNow >= _nextJobWeaponWarning)
            {
                _nextJobWeaponWarning = DateTime.UtcNow.AddMinutes(5);
                OnLog?.Invoke($"Gear: no {requiredType?.ToLowerInvariant() ?? "weapon"} for a {job} and can't afford one yet ({zeny}z) — hunting with what it has.");
            }
            return false;
        }
        var price = _data.Item(pick.Id)!.Price;
        var buy = new List<(int, int)> { (pick.Id, 1) };
        if (pick.Type == "Bow")
        {
            var arrowQty = Math.Min(ArrowStock - arrows, (spendable - price) / arrowPrice);
            if (arrowQty > 0) buy.Add((ArrowItemId, arrowQty));
            SetRestockTarget(ArrowItemId, ArrowStock);
        }
        return TryBuyGear(buy, $"a {job} needs a {requiredType?.ToLowerInvariant() ?? "weapon"}: {_data.ItemName(pick.Id)}, atk {pick.Power}, {price}z");
    }

    private static string SlotLabel(EquipSlot s) => s switch
    {
        EquipSlot.RightHand => "weapon", EquipSlot.LeftHand => "shield", EquipSlot.Body => "armor",
        EquipSlot.Footgear => "shoes", EquipSlot.Garment => "garment", EquipSlot.HeadTop => "hat",
        EquipSlot.HeadMid => "mid headgear", EquipSlot.HeadBottom => "lower headgear", _ => s.ToString(),
    };

    private bool TryBuyGear(int itemId, int qty, string why) => TryBuyGear(new[] { (itemId, qty) }, why);

    // The first item picks the vendor (it must stock it); the rest are bought there too when it has them.
    private bool TryBuyGear(IReadOnlyList<(int ItemId, int Qty)> items, string why)
    {
        if (items.Count == 0 || items[0].Qty <= 0) return false;
        _gearBuyRetryAt[items[0].ItemId] = DateTime.UtcNow.AddMinutes(10); // unreachable vendor / failed trip: don't loop on it
        OnLog?.Invoke($"Gear: buying {string.Join(" + ", items.Select(i => $"{i.Qty}x {_data!.ItemName(i.ItemId)}"))} ({why}).");
        RequestBuyItems(items);
        _customBuyWhy = why;
        return true;
    }
}
