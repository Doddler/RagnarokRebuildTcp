using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Behavior;

/// <summary>
/// Decides which inventory items a bot may auto-sell. Keeps anything equipped, usable (potions/consumables),
/// a card, ammo, refined or carded gear, zero sell-value, on the keep-list, or a configured healing item.
/// Sells junk (Etc loot) and every other unequipped weapon/equipment.
/// </summary>
public static class ShopPolicy
{
    public static bool ShouldSell(ItemData? def, InventoryItemView inv, bool equipped, BotBehaviorConfig cfg)
    {
        if (def == null || equipped) return false;
        if (def.SellPrice <= 0) return false;
        if (cfg.KeepItemIds.Contains(def.Id) || cfg.HealingItemIds.Contains(def.Id)) return false;

        switch (def.ItemClass)
        {
            case ItemClass.Etc:
                return true; // junk loot (Jellopy, Clover, ...) — always sell
            case ItemClass.Weapon:
            case ItemClass.Equipment:
                // A drop an NPC buys isn't rare: only what was put into it (cards, refines) is worth keeping. Neither
                // empty slots (Knife [3]) nor ItemRank (Cotton Shirt is rank 4, sells for 5z) mean rarity, and keeping
                // them filled the bag with kept stacks that held the sell triggers on forever. Upgrades get equipped
                // from the bag by the gear logic, so an unequipped piece is one the bot doesn't want.
                var carded = inv.Cards != null && Array.Exists(inv.Cards, c => c > 0);
                return inv.Refine == 0 && !carded;
            default:
                return false; // Useable, Card, Ammo, None — keep
        }
    }

    /// <summary>
    /// Whether the bot's load or stock should start a natural sell/restock trip. <paramref name="countSaleable"/>
    /// walks the inventory, so it is only invoked when the cheaper thresholds haven't already fired.
    /// </summary>
    public static bool ShouldStartTrip(int weight, int maxWeight, int restockHeld, BotBehaviorConfig cfg, Func<int> countSaleable)
    {
        if (cfg.RestockItemId > 0 && restockHeld < cfg.RestockBelow) return true;
        // Weight only triggers when enough would sell to be worth the walk: a bag full of kept items (cards, food,
        // potions) plus one fresh drop looped the bot to the shop and back every minute. There's no stack trigger: the
        // server has no inventory slot limit, only weight (over 100% the bot can't attack or pick up).
        int? saleable = null;
        int Saleable() => saleable ??= countSaleable();
        var worthATrip = Math.Max(1, cfg.JunkSellCount / 2);
        if (maxWeight > 0 && weight >= maxWeight * cfg.SellWeightPercent && Saleable() >= worthATrip) return true;
        // Saleable-junk trigger: catches the "inventory filling with low-rank weapon drops" case before
        // weight or stack-count thresholds fire. We count items the ShopPolicy would actually sell so the
        // trigger doesn't false-fire on a bot holding equipped/usable/valuable gear.
        if (cfg.JunkSellCount > 0 && Saleable() >= cfg.JunkSellCount) return true;
        // Sell queue and restock-targets list are NOT standalone triggers (the operator queues these
        // ahead of time and doesn't want to disrupt the bot mid-hunt). They merge into the next natural
        // sell trip — restock items as opportunistic buys in DoBuyAsync, sell items in BuildSellOrders.
        // To force a restock trip explicitly, the operator calls force_restock via MCP.
        return false;
    }
}
