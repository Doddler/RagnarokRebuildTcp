using System.Buffers;
using System.Diagnostics;
using Microsoft.Extensions.ObjectPool;
using RoRebuildServer.EntityComponents.Util;
using System.Runtime.InteropServices;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RebuildZoneServer.Networking;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Items;
using System.Linq;

namespace RoRebuildServer.EntityComponents.Items;

public enum BagType
{
    Inventory,
    Cart,
    KafraStorage,
    MonsterInventory
}

public class CharacterBagPool : IPooledObjectPolicy<CharacterBag>
{
    public CharacterBag Create()
    {
        return new CharacterBag();
    }

    public bool Return(CharacterBag obj)
    {
        return obj.TryReset();
    }
}

public class CharacterBag : IResettable
{
    private static ObjectPool<CharacterBag> bagPool = new DefaultObjectPool<CharacterBag>(new CharacterBagPool(), 128);

    public Dictionary<int, RegularItem> RegularItems = new();
    public Dictionary<int, UniqueItem> UniqueItems = new();
    public Dictionary<Guid, int> UniqueItemBagIds = new();

    public int UsedSlots;
    private int idIndex = 10000; //make sure we don't collide with regular item ids

    public static CharacterBag Borrow() => bagPool.Get();
    public static void Return(CharacterBag bag) => bagPool.Return(bag);

    public static CharacterBag FromExisting(IBinaryMessageReader br)
    {
        var bag = Borrow();
        bag.Deserialize(br);
        return bag;
    }

    public bool TryReset()
    {
        RegularItems.Clear();
        UniqueItems.Clear();
        UniqueItemBagIds.Clear();
        UsedSlots = 0;
        idIndex = 10000;
        return true;
    }

    public bool GetItem(int id, out ItemReference item)
    {
        if (RegularItems.TryGetValue(id, out var regularItem))
        {
            item = new ItemReference(regularItem);
            return true;
        }

        if (UniqueItems.TryGetValue(id, out var uniqueItem))
        {
            item = new ItemReference(uniqueItem);
            return true;
        }

        item = default;
        return false;
    }

    public Guid GetGuidByUniqueItemId(int id) => UniqueItems[id].UniqueId;
    
    public int AddItem(ItemReference item)
    {
        if (item.Type == ItemType.RegularItem)
            return AddItem(item.Item);
        else
            return AddItem(item.UniqueItem);
    }
    
    public void AddItem(GroundItem item)
    {
        if (item.Type == ItemType.RegularItem)
            AddItem(item.Item);
        else
            AddItem(item.UniqueItem);
    }

    public int AddItem(RegularItem item)
    {
        if (RegularItems.TryGetValue(item.Id, out var existingItem))
        {
            existingItem.Count += item.Count;
            RegularItems[item.Id] = existingItem;
            return RegularItems[item.Id].Count;
        }

        UsedSlots++;
        RegularItems.Add(item.Id, item);
        return item.Count;
    }

    public int GetUniqueItemIdByGuid(Guid id)
    {
        //boring and slow linear search
        foreach (var item in UniqueItems)
        {
            if (item.Value.UniqueId == id)
                return item.Key;
        }

        return -1;
    }

    public int GetUniqueItemByGuid(Guid id, out UniqueItem itemOut)
    {
        //boring and slow linear search
        foreach (var item in UniqueItems)
        {
            if (item.Value.UniqueId == id)
            {
                itemOut = item.Value;
                return item.Key;
            }
        }

        itemOut = default;
        return -1;
    }
    
    public bool RemoveItemByBagIdAndGetRemovedItem(int id, int removeCount, out ItemReference itemOut)
    {
        if (RegularItems.TryGetValue(id, out var regular))
        {
            var startCount = (int)regular.Count;
            regular.Count = (short)(regular.Count - removeCount);
            if (regular.Count <= 0)
            {
                removeCount = startCount;
                RegularItems.Remove(id);
                UsedSlots--;
            }
            else
                RegularItems[id] = regular;

            itemOut = new ItemReference(new RegularItem() { Id = id, Count = (short) removeCount });
            return true;
        }

        if (UniqueItems.TryGetValue(id, out var unique))
        {
            if (unique.Count != removeCount)
                throw new Exception($"Cannot split stacks on unique items!");
            UniqueItems.Remove(id);
            UniqueItemBagIds.Remove(unique.UniqueId);
            itemOut = new ItemReference(unique);
            UsedSlots--;
            return true;
        }

        itemOut = default;
        return false;
    }

    public bool RemoveItem(RegularItem item)
    {
        if (!RegularItems.TryGetValue(item.Id, out var existing) || existing.Count <= 0)
            return false;

        existing.Count -= item.Count;
        if (existing.Count <= 0)
        {
            RegularItems.Remove(item.Id);
            UsedSlots--;
        }
        else
            RegularItems[item.Id] = existing;
        return true;
    }

    public int AddItem(UniqueItem item)
    {
        if (UniqueItemBagIds.ContainsKey(item.UniqueId))
            throw new Exception($"Attempting to add a unique item a second time to the character's inventory!");

        var id = idIndex;
        UniqueItems.Add(idIndex, item);
        UniqueItemBagIds.Add(item.UniqueId, id);
        UsedSlots++;
        idIndex++;
        return id;
    }

    public bool RemoveUniqueItem(int bagIndex)
    {
        if (!UniqueItems.TryGetValue(bagIndex, out var existing))
            return false;
        UniqueItemBagIds.Remove(existing.UniqueId);
        UniqueItems.Remove(bagIndex);
        UsedSlots--;
        return true;
    }

    public int GetByteSize()
    {
        var size = 16; //4 for version, 4 for regular item count, 4 for unique item count, 4 for sanity check
        if (RegularItems.Count > 0)
            size += RegularItems.Count * RegularItem.Size;

        if (UniqueItems.Count > 0)
            size += UniqueItems.Count * UniqueItem.Size;

        return size;
    }

    public void Deserialize(IBinaryMessageReader br)
    {
        Debug.Assert(UsedSlots == 0);
        Debug.Assert(RegularItems.Count == 0);
        Debug.Assert(UniqueItems.Count == 0);

        br.ReadInt32(); //reserved, if the bag format changes we'll use a version number here to do migrations
        var regularCount = br.ReadInt32();
        for (var i = 0; i < regularCount; i++)
        {
            var item = RegularItem.Deserialize(br);
            RegularItems.Add(item.Id, item);
        }

        var uniqueCount = br.ReadInt32();
        for (var i = 0; i < uniqueCount; i++)
        {
            var id = idIndex;
            idIndex++;
            var item = UniqueItem.Deserialize(br);
            UniqueItems.Add(id, item);
            UniqueItemBagIds.Add(item.UniqueId, id);
        }

        var sanity = br.ReadInt32();

        UsedSlots = regularCount + uniqueCount;
        if (sanity != UsedSlots)
            throw new Exception($"Unable to deserialize character inventory! The number of items was not as we expected.");
    }

    public void Serialize(IBinaryMessageWriter bw, bool writeBagIndexes)
    {
        if(!writeBagIndexes)
            bw.Write(0); //reserved for inventory version number (in case we need to do migrations)... but don't send to client
        bw.Write(RegularItems.Count);
        foreach (var item in RegularItems)
            item.Value.Serialize(bw);

        bw.Write(UniqueItems.Count);
        foreach (var item in UniqueItems)
        {
            if(writeBagIndexes)
                bw.Write(item.Key);
            item.Value.Serialize(bw);
        }

        if(!writeBagIndexes)
            bw.Write(RegularItems.Count + UniqueItems.Count); //for a little safety
    }

    public static CharacterBag? TryRead(IBinaryMessageReader br)
    {
        var exists = br.ReadByte();
        if (exists == 0)
            return null;
        var bag = Borrow();
        bag.Deserialize(br);
        return bag;
    }
}

public static class CharacterBagExtensions
{
    public static int TryGetSize(this CharacterBag? bag)
    {
        if (bag == null)
            return 0;
        return bag.GetByteSize();
    }

    public static void TryWrite(this CharacterBag? bag, IBinaryMessageWriter bw, bool writeBagIndexes)
    {
        if (bag == null || bag.UsedSlots == 0)
            bw.Write((byte)0);
        else
        {
            bw.Write((byte)1);
            bag.Serialize(bw, writeBagIndexes);
        }
    }
}