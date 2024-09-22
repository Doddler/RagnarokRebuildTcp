using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Util;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents.Items;

public struct EquipStatChange : IEquatable<EquipStatChange>
{
    public int Change;
    public CharacterStat Stat;
    public EquipSlot Slot;

    public bool Equals(EquipStatChange other)
    {
        return Stat == other.Stat && Slot == other.Slot;
    }

    public override bool Equals(object? obj)
    {
        return obj is EquipStatChange other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Stat, Change);
    }
}

public class ItemEquipState
{
    public Player Player;
    public int[] ItemSlots = new int[10];
    public int AmmoType;
    private readonly SwapList<EquipStatChange> equipmentEffects = new();
    private EquipSlot activeSlot;


    public void Reset()
    {
        for (var i = 0; i < ItemSlots.Length; i++)
            ItemSlots[i] = 0;
        AmmoType = -1;
    }

    public void OnEquipEvent(EquipSlot slot)
    {
        Debug.Assert(Player.Inventory != null);

        if (ItemSlots[(int)slot] <= 0)
            return;

        activeSlot = slot;
        var bagId = ItemSlots[(int)slot];
        var item = Player.Inventory.UniqueItems[bagId];
        if (!DataManager.ItemList.TryGetValue(item.Id, out var data))
            throw new Exception($"Attempting to run RunAllOnEquip event for item {item.Id}, but it doesn't appear to exist in the item database.");

        data.Interaction?.OnEquip(Player, Player.CombatEntity, this, item, slot);
        for (var j = 0; j < 4; j++)
        {
            unsafe //all this trouble to ensure all 4 slots are always allocated in sequence in the struct
            {
                var slotItem = item.Data[j];
                if (!DataManager.ItemList.TryGetValue(slotItem, out var slotData))
                    throw new Exception($"Attempting to run RunAllOnEquip event for item {item.Id} (socketed in a {item.Id}), but it doesn't appear to exist in the item database."); ;

                slotData.Interaction?.OnEquip(Player, Player.CombatEntity, this, default, slot);
            }
        }
    }

    public void UnEquipEvent(EquipSlot slot)
    {
        Debug.Assert(Player.Inventory != null);

        if (ItemSlots[(int)slot] <= 0)
            return;

        activeSlot = slot;
        var bagId = ItemSlots[(int)slot];
        var item = Player.Inventory.UniqueItems[bagId];
        if (!DataManager.ItemList.TryGetValue(item.Id, out var data))
            throw new Exception($"Attempting to run RunAllOnEquip event for item {item.Id}, but it doesn't appear to exist in the item database.");

        data.Interaction?.OnUnequip(Player, Player.CombatEntity, this, item, slot);
        for (var j = 0; j < 4; j++)
        {
            unsafe //all this trouble to ensure all 4 slots are always allocated in sequence in the struct
            {
                var slotItem = item.Data[j];
                if (slotItem <= 0)
                    continue;

                if (!DataManager.ItemList.TryGetValue(slotItem, out var slotData))
                    throw new Exception($"Attempting to run RunAllOnEquip event for item {item.Id} (socketed in a {item.Id}), but it doesn't appear to exist in the item database."); ;

                slotData.Interaction?.OnUnequip(Player, Player.CombatEntity, this, default, slot);
            }
        }

        //remove saved item effects from the player
        for (var i = 0; i < equipmentEffects.Count; i++)
        {
            var effect = equipmentEffects[i];
            if (effect.Slot == slot)
            {
                Player.CombatEntity.SubStat(effect.Stat, effect.Change);
                equipmentEffects.Remove(i);
                i--; //we've moved the last element into our current position, so we step the enumerator back by 1
            }
        }
    }

    public void RunAllOnEquip()
    {
        if (Player.Inventory == null)
        {
#if DEBUG
            for (var i = 0; i < 10; i++)
                if (ItemSlots[i] > 0)
                    throw new Exception($"Player inventory is empty, but we still have items in our equip state!");
#endif
            return;
        }

        for (var i = 0; i < 10; i++)
            OnEquipEvent((EquipSlot)i);
    }

    public void AddStat(CharacterStat stat, int change)
    {
#if DEBUG
        if(stat >= CharacterStat.Str && stat <= CharacterStat.Luk)
            ServerLogger.LogWarning($"Warning! Adding directly to a base stat {stat} in equip handler for {Player.Inventory?.UniqueItems[(int)activeSlot]}! You probably want AddStat.");
#endif
        var equipState = new EquipStatChange()
        {
            Slot = activeSlot,
            Change = change,
            Stat = stat
        };
        equipmentEffects.Add(ref equipState);
        Player.CombatEntity.AddStat(stat, change);
    }

    public void SubStat(CharacterStat stat, int change) => AddStat(stat, -change); //lol

    public void Serialize(IBinaryMessageWriter bw)
    {
        if (Player.Inventory == null)
            return;

        foreach (var itemId in ItemSlots)
        {
            bw.Write(itemId > 0);
            if (itemId > 0)
                bw.Write(Player.Inventory.GetGuidByUniqueItemId(itemId).ToByteArray()); //we have a bag id, we want to store the guid
        }
        bw.Write(AmmoType);
    }

    public void DeSerialize(IBinaryMessageReader br, CharacterBag bag)
    {
        for (var i = 0; i < 10; i++)
        {
            if (br.ReadBoolean())
            {
                var guid = new Guid(br.ReadBytes(16)); //we have a guid, we want to store a bag id
                var itemId = bag.GetUniqueItemIdByGuid(guid);
                if (itemId > 0)
                    ItemSlots[i] = itemId;
            }
        }

        AmmoType = br.ReadInt32();
    }
}