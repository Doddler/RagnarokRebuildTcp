using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Behavior;

/// <summary>
/// Decides which inventory items a bot may auto-sell. Conservative by design: keeps anything equipped,
/// usable (potions/consumables), a card, ammo, refined/carded/slotted (i.e. potentially valuable) gear,
/// zero sell-value, on the keep-list, or a configured healing item. Sells junk (Etc loot) and plain
/// common weapons/equipment (unrefined, uncarded, unslotted, normal rank).
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
                var carded = inv.Cards != null && Array.Exists(inv.Cards, c => c > 0);
                return inv.Refine == 0 && !carded && def.Slots == 0 && def.ItemRank <= 0;
            default:
                return false; // Useable, Card, Ammo, None — keep
        }
    }

    /// <summary>
    /// Whether the bot's load or stock should start a natural sell/restock trip. <paramref name="countSaleable"/>
    /// walks the inventory, so it is only invoked when the cheaper thresholds haven't already fired.
    /// </summary>
    public static bool ShouldStartTrip(int weight, int maxWeight, int stacks, int restockHeld, BotBehaviorConfig cfg, Func<int> countSaleable)
    {
        if (maxWeight > 0 && weight >= maxWeight * cfg.SellWeightPercent) return true;
        // Only when something would sell: kept stacks (slotted gear, cards) alone looped the bot to a shop and back.
        if (stacks >= cfg.SellItemStacks && countSaleable() > 0) return true;
        if (cfg.RestockItemId > 0 && restockHeld < cfg.RestockBelow) return true;
        // Saleable-junk trigger: catches the "inventory filling with low-rank weapon drops" case before
        // weight or stack-count thresholds fire. We count items the ShopPolicy would actually sell so the
        // trigger doesn't false-fire on a bot holding equipped/usable/valuable gear.
        if (cfg.JunkSellCount > 0 && countSaleable() >= cfg.JunkSellCount) return true;
        // Sell queue and restock-targets list are NOT standalone triggers (the operator queues these
        // ahead of time and doesn't want to disrupt the bot mid-hunt). They merge into the next natural
        // sell trip — restock items as opportunistic buys in DoBuyAsync, sell items in BuildSellOrders.
        // To force a restock trip explicitly, the operator calls force_restock via MCP.
        return false;
    }
}
