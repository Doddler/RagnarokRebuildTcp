using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Logging;

namespace RoRebuildServer.EntityComponents.Npcs;

public class NpcTradeItem
{
    public int ItemId;
    public List<int>? Sockets;
    public int ZenyCost;
    public int TradeCount = 1;
    public List<(int, int)> ItemRequirements = new();
    public bool IsCrafted;
    public ItemReference CombinedItem;

    public NpcTradeItem(string itemName)
    {
        if(!DataManager.ItemIdByName.TryGetValue(itemName, out var itemId))
            ServerLogger.LogWarning($"Failed to create NpcTradeItem, the requested item type {itemName} was invalid!");
        else
            ItemId = DataManager.ItemIdByName[itemName];
    }

    public NpcTradeItem Socket(string itemName)
    {
        if (!DataManager.ItemIdByName.TryGetValue(itemName, out var itemId))
        {
            ServerLogger.LogWarning($"Failed to socket item into NpcTradeItem, the requested item type {itemName} was invalid!");
            return this;
        }

        if (Sockets == null)
            Sockets = new List<int>();
        
        Sockets.Add(DataManager.ItemIdByName[itemName]);
        return this;
    }

    public NpcTradeItem Requires(string itemName, int count = 1)
    {
        if (!DataManager.ItemIdByName.TryGetValue(itemName, out var itemId))
        {
            ServerLogger.LogWarning($"Failed to set required item for NpcTradeItem, the item type {itemName} was invalid!");
            return this;
        }

        ItemRequirements.Add((itemId, count));
        return this;
    }

    public NpcTradeItem Costs(int cost)
    {
        ZenyCost = cost;
        return this;
    }

    public void FinalizeItem()
    {;
        CombinedItem = new ItemReference(ItemId, TradeCount);
        var isUnique = CombinedItem.Type == ItemType.UniqueItem;

        if (IsCrafted && isUnique)
            CombinedItem.UniqueItem.Flags = (byte)UniqueItemFlags.CraftedItem;

        if (Sockets == null || !isUnique)
            return;

        var sockets = Sockets.Count;
        if (sockets > 4)
        {
            ServerLogger.LogWarning($"You've attempted to assign more than 4 items to sockets on the NpcTradeItem id {ItemId}!");
            sockets = 4;
        }

        for (var i = 0; i < sockets; i++) 
            CombinedItem.UniqueItem.SetSlotData(i, Sockets[i]);
    }
}