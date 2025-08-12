using System.Diagnostics;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Util;
using RoRebuildServer.Data;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using Wintellect.PowerCollections;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RoRebuildServer.EntityComponents.Items;

public struct EquipStatChange : IEquatable<EquipStatChange>
{
    public int Value;
    public int Change;
    public CharacterStat Stat;
    public int Slot;

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

public struct AutoSpellEffect
{
    public CharacterSkill Skill;
    public SkillPreferredTarget Target;
    public int Level;
    public int Chance;
}

public class ItemEquipState
{
    public Player Player = null!; //this is set in player init
    public readonly int[] ItemSlots = new int[10];
    public readonly int[] ItemIds = new int[10];
    public int AmmoId;
    public AmmoType AmmoType;
    public int AmmoAttackPower;
    public bool IsDualWielding;
    public int DoubleAttackModifiers;
    public int WeaponRange;
    public AttackElement WeaponElement;
    public AttackElement AmmoElement;
    public CharacterElement ArmorElement;
    public int WeaponLevel;
    public int WeaponAttackPower;
    public int MinRefineAtkBonus;
    public int MaxRefineAtkBonus;
    public Dictionary<string, int> ActiveItemCombos = new();
    public Dictionary<int, int> EquippedItems = new();
    public Dictionary<int, AutoSpellEffect> AutoSpellSkillsOnAttack = new();
    public Dictionary<int, AutoSpellEffect> AutoSpellSkillsWhenAttacked = new();
    private readonly SwapList<EquipStatChange> equipmentEffects = new();
    private int activeSlotId;
    private readonly HeadgearPosition[] headgearMultiSlotInfo = new HeadgearPosition[3];
    private bool isTwoHandedWeapon;
    private static int[] attackPerRefine = [2, 3, 5, 7];
    private static int[] overRefineLevel = [7, 6, 5, 4];
    private static int[] overRefineAttackBonus = [3, 5, 8, 14];
    private int nextId = 0;
    private int nextComboSlotId = 100;

    public void Reset()
    {
        for (var i = 0; i < ItemSlots.Length; i++)
            ItemSlots[i] = 0;
        for (var i = 0; i < 3; i++)
            headgearMultiSlotInfo[i] = 0;
        AmmoId = -1;
        WeaponRange = 1;
        WeaponLevel = 0;
        WeaponAttackPower = 0;
        MinRefineAtkBonus = 0;
        MaxRefineAtkBonus = 0;
        WeaponElement = AttackElement.Neutral;
        ArmorElement = CharacterElement.Neutral1;
        AmmoElement = AttackElement.None;
        AutoSpellSkillsOnAttack.Clear();
        AutoSpellSkillsWhenAttacked.Clear();
        equipmentEffects.Clear();
        EquippedItems.Clear();
        ActiveItemCombos.Clear();
        nextId = 0;
    }

    public int GetEquipmentIdBySlot(EquipSlot slot) => ItemIds[(int)slot];

    public bool IsItemEquipped(int bagId)
    {
        for (var i = 0; i < 10; i++)
            if (ItemSlots[i] == bagId)
                return true;
        return false;
    }


    public bool IsItemIdEquipped(int itemId)
    {
        if (AmmoId == itemId)
            return true;
        for (var i = 0; i < 10; i++)
            if (ItemIds[i] == itemId)
                return true;
        return false;
    }

    public EquipSlot GetOccupiedSlotForItem(int bagId)
    {
        for (var i = 0; i < 10; i++)
            if (ItemSlots[i] == bagId)
                return (EquipSlot)i;
        return EquipSlot.None;
    }

    public void UnequipAllItems()
    {
        for (var i = 0; i < 10; i++)
        {
            if (ItemSlots[i] <= 0)
                continue;

            UnEquipEvent((EquipSlot)i);
            CommandBuilder.PlayerEquipItem(Player, ItemSlots[i], (EquipSlot)i, false);

            ItemSlots[i] = -1;
            ItemIds[i] = -1;
            IsDualWielding = false;
            isTwoHandedWeapon = false;
        }
        for (var i = 0; i < 3; i++)
            headgearMultiSlotInfo[i] = HeadgearPosition.None;

        RemoveEquipEffectForAmmo();
        AmmoId = -1;
    }

    public void UpdateAppearanceIfNecessary(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.HeadTop:
            case EquipSlot.HeadBottom:
            case EquipSlot.HeadMid:
            case EquipSlot.Weapon:
            case EquipSlot.Shield:
                CommandBuilder.UpdatePlayerAppearanceAuto(Player);
                break;
        }
    }

    public void UnEquipItem(int bagId)
    {
        if (Player.Inventory == null || !Player.Inventory.GetItem(bagId, out var item))
            return;

        var itemData = DataManager.ItemList[item.Id];
        var updateAppearance = false;

        if (itemData.ItemClass == ItemClass.Ammo)
        {
            RemoveEquipEffectForAmmo();
            AmmoId = -1;
            AmmoAttackPower = 0;
            AmmoElement = AttackElement.None;
            CommandBuilder.PlayerEquipItem(Player, bagId, EquipSlot.Ammunition, false);
            return;
        }

        if (itemData.ItemClass == ItemClass.Weapon)
        {
            UnEquipItem(EquipSlot.Weapon);
            updateAppearance = true;
        }
        else
        {
            var equipInfo = DataManager.ArmorInfo[item.Id];
            if (equipInfo.EquipPosition.HasFlag(EquipPosition.Headgear))
            {
                if (equipInfo.HeadPosition.HasFlag(HeadgearPosition.Top)) UnEquipItem(EquipSlot.HeadTop);
                if (equipInfo.HeadPosition.HasFlag(HeadgearPosition.Mid)) UnEquipItem(EquipSlot.HeadMid);
                if (equipInfo.HeadPosition.HasFlag(HeadgearPosition.Bottom)) UnEquipItem(EquipSlot.HeadBottom);
                updateAppearance = true;
            }

            if (equipInfo.EquipPosition.HasFlag(EquipPosition.Shield))
            {
                UnEquipItem(EquipSlot.Shield);
                updateAppearance = true;
            }
            if (equipInfo.EquipPosition.HasFlag(EquipPosition.Armor)) UnEquipItem(EquipSlot.Body);
            if (equipInfo.EquipPosition.HasFlag(EquipPosition.Garment)) UnEquipItem(EquipSlot.Garment);
            if (equipInfo.EquipPosition.HasFlag(EquipPosition.Boots)) UnEquipItem(EquipSlot.Footgear);
            if (equipInfo.EquipPosition.HasFlag(EquipPosition.Accessory))
            {
                if (ItemSlots[(int)EquipSlot.Accessory1] == bagId) UnEquipItem(EquipSlot.Accessory1);
                if (ItemSlots[(int)EquipSlot.Accessory2] == bagId) UnEquipItem(EquipSlot.Accessory2);
            }
        }

        if (Player.CombatEntity.TryGetStatusContainer(out var status))
            status.OnChangeEquipment();
        if (updateAppearance)
            CommandBuilder.UpdatePlayerAppearanceAuto(Player);
        Player.UpdateStats();
    }

    private void UnEquipItem(EquipSlot slot)
    {
        var bagId = ItemSlots[(int)slot];
        if (bagId <= 0)
            return;

        UnEquipEvent(slot);
        ItemSlots[(int)slot] = 0;
        ItemIds[(int)slot] = 0;
        CommandBuilder.PlayerEquipItem(Player, bagId, slot, false);
        if (slot == EquipSlot.Weapon)
        {
            isTwoHandedWeapon = false;
            WeaponRange = 1;
        }

        if (slot == EquipSlot.Shield)
            IsDualWielding = false; //probably not but may as well
        if (slot == EquipSlot.HeadTop || slot == EquipSlot.HeadMid || slot == EquipSlot.HeadBottom)
            headgearMultiSlotInfo[(int)slot] = HeadgearPosition.None;
    }

    private EquipSlot EquipSlotForWeapon(WeaponInfo weapon)
    {
        //if an assassin is using a one-handed weapon and they are currently equipped with one weapon and no shield, place in shield slot
        if (Player.Character.ClassId == 11 && !weapon.IsTwoHanded && ItemSlots[(int)EquipSlot.Weapon] > 0 &&
            ItemSlots[(int)EquipSlot.Shield] == 0)
            return EquipSlot.Shield;
        return EquipSlot.Weapon;
    }

    private EquipSlot EquipSlotForEquipment(ArmorInfo info, int itemId)
    {
        if ((info.EquipPosition & EquipPosition.Headgear) != 0)
        {
            if (info.HeadPosition.HasFlag(HeadgearPosition.Top)) return EquipSlot.HeadTop;
            if (info.HeadPosition.HasFlag(HeadgearPosition.Mid)) return EquipSlot.HeadMid;
            if (info.HeadPosition.HasFlag(HeadgearPosition.Bottom)) return EquipSlot.HeadBottom;
        }
        if (info.EquipPosition.HasFlag(EquipPosition.Body)) return EquipSlot.Body;
        if (info.EquipPosition.HasFlag(EquipPosition.Garment)) return EquipSlot.Garment;
        if (info.EquipPosition.HasFlag(EquipPosition.Shield)) return EquipSlot.Shield;
        if (info.EquipPosition.HasFlag(EquipPosition.Boots)) return EquipSlot.Footgear;
        if (info.EquipPosition.HasFlag(EquipPosition.Accessory))
            if (ItemSlots[(int)EquipSlot.Accessory1] > 0 && ItemSlots[(int)EquipSlot.Accessory2] <= 0)
                return EquipSlot.Accessory2;
            else
                return EquipSlot.Accessory1;

        throw new Exception($"Invalid equipment position for item {itemId}!");
    }

    private bool IsValidSlotForEquipment(ArmorInfo info, EquipSlot slot)
    {
        return slot switch
        {
            EquipSlot.HeadTop => (info.EquipPosition & EquipPosition.Headgear) > 0 && info.HeadPosition.HasFlag(HeadSlots.Top),
            EquipSlot.HeadMid => (info.EquipPosition & EquipPosition.Headgear) > 0 && info.HeadPosition.HasFlag(HeadSlots.Mid),
            EquipSlot.HeadBottom => (info.EquipPosition & EquipPosition.Headgear) > 0 && info.HeadPosition.HasFlag(HeadSlots.Bottom),
            EquipSlot.Weapon => false,
            EquipSlot.Shield => info.EquipPosition.HasFlag(EquipPosition.Shield),
            EquipSlot.Body => info.EquipPosition.HasFlag(EquipPosition.Armor),
            EquipSlot.Garment => info.EquipPosition.HasFlag(EquipPosition.Garment),
            EquipSlot.Footgear => info.EquipPosition.HasFlag(EquipPosition.Footgear),
            EquipSlot.Accessory1 => info.EquipPosition.HasFlag(EquipPosition.Accessory),
            EquipSlot.Accessory2 => info.EquipPosition.HasFlag(EquipPosition.Accessory),
            _ => false
        };
    }

    private EquipChangeResult EquipWeapon(int bagId, EquipSlot equipSlot, ItemInfo itemData)
    {
        var weaponInfo = DataManager.WeaponInfo[itemData.Id];

        if (equipSlot == EquipSlot.None)
            equipSlot = EquipSlotForWeapon(weaponInfo);

        if (equipSlot != EquipSlot.Weapon && equipSlot != EquipSlot.Shield)
            return EquipChangeResult.InvalidItem;

        if (Player.GetStat(CharacterStat.Level) < weaponInfo.MinLvl)
            return EquipChangeResult.LevelTooLow;

        if (!DataManager.IsJobInEquipGroup(weaponInfo.EquipGroup, Player.Character.ClassId))
            return EquipChangeResult.NotApplicableJob;

        if (equipSlot == EquipSlot.Shield && Player.Character.ClassId != 11 && !weaponInfo.IsTwoHanded)
            return EquipChangeResult.InvalidItem;

        //make sure they don't equip the same weapon in both hands
        if ((equipSlot == EquipSlot.Shield && ItemSlots[(int)EquipSlot.Weapon] == bagId)
            || (equipSlot == EquipSlot.Weapon && ItemSlots[(int)EquipSlot.Shield] == bagId))
            return EquipChangeResult.InvalidItem;

        if (weaponInfo.IsTwoHanded)
            UnEquipItem(EquipSlot.Shield);

        UnEquipItem(equipSlot);

        IsDualWielding = equipSlot == EquipSlot.Shield;
        isTwoHandedWeapon = weaponInfo.IsTwoHanded;

        ItemSlots[(int)equipSlot] = bagId;
        ItemIds[(int)equipSlot] = itemData.Id;

        OnEquipEvent(equipSlot);
        CommandBuilder.UpdatePlayerAppearanceAuto(Player);

        if (Player.CombatEntity.TryGetStatusContainer(out var status))
            status.OnChangeEquipment();

        CommandBuilder.PlayerEquipItem(Player, bagId, equipSlot, true);

        return EquipChangeResult.Success;
    }

    public EquipChangeResult EquipArmorOrAccessory(int bagId, EquipSlot equipSlot, ItemInfo itemData)
    {
        var equipInfo = DataManager.ArmorInfo[itemData.Id];
        if (Player.GetStat(CharacterStat.Level) < equipInfo.MinLvl)
            return EquipChangeResult.LevelTooLow;

        if (!DataManager.IsJobInEquipGroup(equipInfo.EquipGroup, Player.Character.ClassId))
            return EquipChangeResult.NotApplicableJob;


        if (equipSlot == EquipSlot.None || equipInfo.EquipPosition == EquipPosition.Headgear)
            equipSlot = EquipSlotForEquipment(equipInfo, itemData.Id);
        else if (!IsValidSlotForEquipment(equipInfo, equipSlot))
            return EquipChangeResult.InvalidPosition;

        //make sure they don't equip the same accessory in both slots
        if (equipSlot == EquipSlot.Accessory1)
        {
            if (ItemSlots[(int)EquipSlot.Accessory2] == bagId)
                return EquipChangeResult.InvalidItem;
        }
        else if (equipSlot == EquipSlot.Accessory2)
        {
            if (ItemSlots[(int)EquipSlot.Accessory1] == bagId)
                return EquipChangeResult.InvalidItem;
        }

        if (equipInfo.EquipPosition == EquipPosition.Shield && isTwoHandedWeapon)
            UnEquipItem(EquipSlot.Weapon);

        UnEquipItem(equipSlot);

        if (equipInfo.EquipPosition == EquipPosition.Headgear)
        {
            for (var i = 0; i < 3; i++)
            {
                //if any of the other headgear block this slot, we'll need to unequip them as well
                if ((equipInfo.HeadPosition & headgearMultiSlotInfo[i]) > 0)
                    UnEquipItem((EquipSlot)i);
            }

            headgearMultiSlotInfo[(int)equipSlot] = equipInfo.HeadPosition;
        }

        ItemSlots[(int)equipSlot] = bagId;
        ItemIds[(int)equipSlot] = itemData.Id;

        OnEquipEvent(equipSlot);
        CommandBuilder.UpdatePlayerAppearanceAuto(Player);

        if (Player.CombatEntity.TryGetStatusContainer(out var status))
            status.OnChangeEquipment();

        CommandBuilder.PlayerEquipItem(Player, bagId, equipSlot, true);

        return EquipChangeResult.Success;
    }

    public EquipChangeResult EquipItem(int bagId, EquipSlot equipSlot = EquipSlot.None)
    {
        if (Player.Inventory == null || !Player.Inventory.GetItem(bagId, out var item))
            return EquipChangeResult.InvalidItem;

        var itemData = DataManager.ItemList[item.Id];

        if (itemData.ItemClass == ItemClass.Ammo)
            return EquipAmmo(item.Id) ? EquipChangeResult.Success : EquipChangeResult.InvalidItem;

        if (IsItemEquipped(bagId))
            return EquipChangeResult.AlreadyEquipped;

        if (itemData.ItemClass == ItemClass.Weapon)
            return EquipWeapon(bagId, equipSlot, itemData);

        if (itemData.ItemClass == ItemClass.Equipment)
            return EquipArmorOrAccessory(bagId, equipSlot, itemData);

        return EquipChangeResult.InvalidItem;
    }

    private void RemoveEquipEffectForAmmo()
    {
        if (!DataManager.ItemList.TryGetValue(AmmoId, out var unEquipInfo))
            return;

        for (var i = 0; i < equipmentEffects.Count; i++)
        {
            var effect = equipmentEffects[i];
            if (effect.Slot == (int)EquipSlot.Ammunition)
            {
                ReverseEquipmentEffect(effect);
                equipmentEffects.Remove(i);
                i--; //we've moved the last element into our current position, so we step the enumerator back by 1
            }
        }

        SubEquipItemCount(AmmoId);

        unEquipInfo.Interaction?.OnUnequip(Player, Player.CombatEntity, this, new UniqueItem(),
            EquipSlot.Ammunition);
        OnUnEquipUpdateItemSets(AmmoId);
    }

    public bool EquipAmmo(int ammoId, bool isPlayerActive = true, bool unEquipExisting = true, bool forceUpdate = false)
    {
        if (AmmoId == ammoId && !forceUpdate)
            return true;

        if (!DataManager.AmmoInfo.TryGetValue(ammoId, out var ammo))
            return false;

        //player can be null here if we're deserializing on character load. In that case, the OnEquip event will be sent from RunEquipAll
        if (isPlayerActive && unEquipExisting)
            RemoveEquipEffectForAmmo();

        AmmoId = ammoId;
        AmmoType = ammo.Type;
        AmmoAttackPower = ammo.Attack;
        AmmoElement = ammo.Element;

        if (isPlayerActive)
        {
            AddEquipItemCount(AmmoId);
            CommandBuilder.PlayerEquipItem(Player, ammoId, EquipSlot.Ammunition, true);
            if (DataManager.ItemList.TryGetValue(ammoId, out var equipInfo))
                equipInfo.Interaction?.OnEquip(Player, Player.CombatEntity, this, new UniqueItem(), EquipSlot.Ammunition);
            OnEquipUpdateItemSets(AmmoId);
        }

        return true;
    }

    private void OnEquipEvent(EquipSlot slot)
    {
        Debug.Assert(Player.Inventory != null);

        if (slot == EquipSlot.None || ItemSlots[(int)slot] <= 0)
            return;

        activeSlotId = (int)slot;
        var bagId = ItemSlots[(int)slot];
        var item = Player.Inventory.UniqueItems[bagId];
        if (!DataManager.ItemList.TryGetValue(item.Id, out var data))
        {
            ServerLogger.LogWarning($"Player {Player.Character.Name} has an itemId {item} equipped but we don't have such an item in our item database.");
            return;
        }

        if (data.ItemClass == ItemClass.Weapon)
        {
            if (!DataManager.WeaponInfo.TryGetValue(item.Id, out var weapon))
            {
                Player.WeaponClass = 0;
                Player.SetStat(CharacterStat.Attack, 0);
                Player.SetStat(CharacterStat.Attack2, 0);
                Player.RefreshWeaponMastery();
                WeaponElement = AttackElement.Neutral;
                WeaponRange = 1;
            }
            else
            {
                Player.WeaponClass = weapon.WeaponClass;
                WeaponLevel = weapon.WeaponLevel - 1;

                var refBonus = item.Refine * attackPerRefine[WeaponLevel];
                var overRefBonus = 0;
                var overRefine = item.Refine - overRefineLevel[WeaponLevel];
                if (overRefine > 0)
                    overRefBonus = overRefine * overRefineAttackBonus[WeaponLevel];

                WeaponAttackPower = weapon.Attack;
                MinRefineAtkBonus = refBonus;
                MaxRefineAtkBonus = refBonus + overRefBonus;
                Player.SetStat(CharacterStat.Attack, 0);
                Player.SetStat(CharacterStat.Attack2, 0);
                Player.RefreshWeaponMastery();
                WeaponElement = weapon.Element;
                if (slot == EquipSlot.Weapon)
                    WeaponRange = weapon.Range;
            }
        }

        if (data.ItemClass == ItemClass.Equipment)
        {
            if (DataManager.ArmorInfo.TryGetValue(item.Id, out var armor))
            {
                Player.AddStat(CharacterStat.Def, armor.Defense);
                Player.AddStat(CharacterStat.MDef, armor.MagicDefense);

                if (armor.IsRefinable)
                    Player.AddStat(CharacterStat.EquipmentRefineDef, item.Refine);

                if (armor.EquipPosition == EquipPosition.Body)
                    ArmorElement = armor.Element;
            }
        }

        AddEquipItemCount(item.Id);

        data.Interaction?.OnEquip(Player, Player.CombatEntity, this, item, slot);
        for (var j = 0; j < 4; j++)
        {
            unsafe //all this trouble to ensure all 4 slots are always allocated in sequence in the struct
            {
                var slotItem = item.Data[j];
                if (slotItem <= 0)
                    continue;
                if (!DataManager.ItemList.TryGetValue(slotItem, out var slotData))
                    throw new Exception($"Attempting to run RunAllOnEquip event for item {slotItem} (socketed in a {item.Id}), but it doesn't appear to exist in the item database.");

                AddEquipItemCount(slotData.Id);
                slotData.Interaction?.OnEquip(Player, Player.CombatEntity, this, default, slot);
                OnEquipUpdateItemSets(slotData.Id);
            }
        }

        OnEquipUpdateItemSets(item.Id);
    }

    private void AddEquipItemCount(int itemId)
    {
        if (EquippedItems.TryGetValue(itemId, out var equipCount))
            EquippedItems[itemId] = equipCount + 1;
        else
            EquippedItems.Add(itemId, 1);
    }

    private void SubEquipItemCount(int itemId)
    {
        if (EquippedItems.TryGetValue(itemId, out var equipCount))
        {
            if (equipCount > 1)
                EquippedItems[itemId] = equipCount - 1;
            else
                EquippedItems.Remove(itemId);
        }
        else
            ServerLogger.LogWarning($"Attempting to perform SubEquipItemCount for itemId {itemId}, but we don't think that item is currently equipped.");
    }

    public void PerformOnEquipForNewCard(ItemInfo item, EquipSlot slot)
    {
        activeSlotId = (int)slot;
        AddEquipItemCount(item.Id);
        item.Interaction?.OnEquip(Player, Player.CombatEntity, this, default, slot);
        OnEquipUpdateItemSets(item.Id);
    }

    private void UnEquipEvent(EquipSlot slot)
    {
        Debug.Assert(Player.Inventory != null);

        if (ItemSlots[(int)slot] <= 0)
            return;

        activeSlotId = (int)slot;
        var bagId = ItemSlots[(int)slot];
        var item = Player.Inventory.UniqueItems[bagId];
        if (!DataManager.ItemList.TryGetValue(item.Id, out var data))
            throw new Exception($"Attempting to run RunAllOnEquip event for item {item.Id}, but it doesn't appear to exist in the item database.");

        if (data.ItemClass == ItemClass.Weapon)
        {
            Player.WeaponClass = 0;
            WeaponLevel = 0;
            WeaponAttackPower = 0;
            MinRefineAtkBonus = 0;
            MaxRefineAtkBonus = 0;
            WeaponElement = AttackElement.Neutral;
            Player.RefreshWeaponMastery();
        }

        if (data.ItemClass == ItemClass.Equipment)
        {
            if (DataManager.ArmorInfo.TryGetValue(item.Id, out var armor))
            {
                Player.SubStat(CharacterStat.Def, armor.Defense);
                Player.SubStat(CharacterStat.MDef, armor.MagicDefense);

                if (armor.IsRefinable)
                    Player.SubStat(CharacterStat.EquipmentRefineDef, item.Refine);

                if (armor.EquipPosition == EquipPosition.Body)
                    ArmorElement = CharacterElement.Neutral1;
            }
        }

        SubEquipItemCount(item.Id);
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

                SubEquipItemCount(slotData.Id);
                slotData.Interaction?.OnUnequip(Player, Player.CombatEntity, this, default, slot);
                OnUnEquipUpdateItemSets(slotData.Id);
            }
        }

        ReverseEquipEffectsForSlot((int)slot);
        OnUnEquipUpdateItemSets(item.Id);
    }

    private void ReverseEquipEffectsForSlot(int slot)
    {
        var removedGrantedSkill = false;
        //remove saved item effects from the player
        for (var i = 0; i < equipmentEffects.Count; i++)
        {
            var effect = equipmentEffects[i];
            if (effect.Slot == slot)
            {
                removedGrantedSkill = ReverseEquipmentEffect(effect);
                equipmentEffects.Remove(i);
                i--; //we've moved the last element into our current position, so we step the enumerator back by 1
            }
        }
        if (removedGrantedSkill)
            CommandBuilder.RefreshGrantedSkills(Player);
    }

    private bool ReverseEquipmentEffect(EquipStatChange effect)
    {
        switch (effect.Stat)
        {
            case CharacterStat.SkillValue:
                Player.RemoveGrantedSkill((CharacterSkill)effect.Value, effect.Change);
                return true;
            case CharacterStat.AutoSpellOnAttacking:
                AutoSpellSkillsOnAttack.Remove(effect.Change);
                return false;
            case CharacterStat.AutoSpellWhenAttacked:
                AutoSpellSkillsWhenAttacked.Remove(effect.Change);
                return false;
            case CharacterStat.DamageVsTag:
                if (Player.AttackVersusTag != null &&
                    Player.AttackVersusTag.TryGetValue(effect.Value, out var existingAttack))
                {
                    var newVal = existingAttack - effect.Change;
                    if (newVal == 0)
                        Player.AttackVersusTag.Remove(effect.Value);
                    else
                        Player.AttackVersusTag[effect.Value] = newVal;
                }
                return false;
            case CharacterStat.ResistVsTag:
                if (Player.ResistVersusTag != null &&
                    Player.ResistVersusTag.TryGetValue(effect.Value, out var existingResist))
                {
                    var newVal = existingResist - effect.Change;
                    if (newVal == 0)
                        Player.ResistVersusTag.Remove(effect.Value);
                    else
                        Player.ResistVersusTag[effect.Value] = newVal;
                }
                return false;
            case CharacterStat.DoubleAttackChance:
                DoubleAttackModifiers--;
                goto default;
            default:
                Player.CombatEntity.SubStat(effect.Stat, effect.Change);
                return false;
        }
    }

    private bool IsComboPrereqsMet(string comboName, int curItem = -1)
    {

        var itemsInSet = DataManager.ItemsInCombo[comboName];

        foreach (var item in itemsInSet)
        {
            if (item == curItem)
                continue;

            if (!EquippedItems.ContainsKey(item))
                return false;
        }

        return true;
    }

    private void OnEquipUpdateItemSets(int itemId)
    {
        if (!DataManager.CombosForEquipmentItem.TryGetValue(itemId, out var comboNameList))
            return;

        var oldActiveSlot = activeSlotId; //don't want to cause issues if we call OnEquipUpdateItemSets on cards

        foreach (var comboName in comboNameList)
        {
            if (ActiveItemCombos.ContainsKey(comboName))
                continue;

            if (!IsComboPrereqsMet(comboName, itemId))
                continue;

            activeSlotId = nextComboSlotId;
            DataManager.EquipmentComboInteractions[comboName].OnEquip(Player, Player.CombatEntity, this, default, EquipSlot.None);

            ActiveItemCombos.Add(comboName, nextComboSlotId);
            nextComboSlotId++;
        }

        activeSlotId = oldActiveSlot;
    }

    private void OnUnEquipUpdateItemSets(int itemId)
    {
        if (!DataManager.CombosForEquipmentItem.TryGetValue(itemId, out var comboNameList))
            return;

        var oldActiveSlot = activeSlotId; //don't want to cause issues if we call OnUnEquipUpdateItemSets on cards

        foreach (var comboName in comboNameList)
        {
            if (!ActiveItemCombos.TryGetValue(comboName, out var comboSlot))
                continue;

            if (IsComboPrereqsMet(comboName))
                continue;

            activeSlotId = comboSlot;
            DataManager.EquipmentComboInteractions[comboName].OnUnequip(Player, Player.CombatEntity, this, default, EquipSlot.None);
            ActiveItemCombos.Remove(comboName);

            ReverseEquipEffectsForSlot(comboSlot);
        }

        activeSlotId = oldActiveSlot;
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

        if (AmmoId > 0 && DataManager.ItemList.TryGetValue(AmmoId, out var equipInfo))
        {
            EquipAmmo(AmmoId, true, false, true);
            //equipInfo.Interaction?.OnEquip(Player, Player.CombatEntity, this, new UniqueItem(), EquipSlot.Ammunition);
        }
    }

    public int Refine
    {
        get
        {
            if (ItemIds[(int)activeSlotId] <= 0 || Player.Inventory == null)
                return 0;
            if (!Player.Inventory.UniqueItems.TryGetValue(ItemSlots[(int)activeSlotId], out var item))
                return 0;

            return (int)item.Refine;
        }
    }

    public bool IsInPosition(EquipPosition pos)
    {
        switch ((EquipSlot)activeSlotId)
        {
            case EquipSlot.HeadTop:
                return (pos & EquipPosition.HeadUpper) > 0;
            case EquipSlot.HeadMid:
                return (pos & EquipPosition.HeadMid) > 0;
            case EquipSlot.HeadBottom:
                return (pos & EquipPosition.HeadLower) > 0;
            case EquipSlot.Body:
                return (pos & EquipPosition.Body) > 0;
            case EquipSlot.Weapon:
                return (pos & EquipPosition.Weapon) > 0;
            case EquipSlot.Shield:
                return (pos & EquipPosition.Shield) > 0;
            case EquipSlot.Garment:
                return (pos & EquipPosition.Garment) > 0;
            case EquipSlot.Footgear:
                return (pos & EquipPosition.Footgear) > 0;
            case EquipSlot.Accessory1:
            case EquipSlot.Accessory2:
                return (pos & EquipPosition.Accessory) > 0;
        }

        return false;
    }

    public void AutoSpellOnAttack(CharacterSkill skill, int level, int chance, SkillPreferredTarget target = SkillPreferredTarget.Any)
    {
        var cast = new AutoSpellEffect()
        {
            Skill = skill,
            Level = level,
            Chance = chance,
            Target = target
        };

        var id = nextId++;
        AutoSpellSkillsOnAttack.Add(id, cast);

        var equipState = new EquipStatChange()
        {
            Slot = (int)activeSlotId,
            Stat = CharacterStat.AutoSpellOnAttacking,
            Value = (int)skill,
            Change = id,
        };

        equipmentEffects.Add(ref equipState);
    }

    public void AutoSpellWhenAttacked(CharacterSkill skill, int level, int chance, SkillPreferredTarget target = SkillPreferredTarget.Any)
    {
        var cast = new AutoSpellEffect()
        {
            Skill = skill,
            Level = level,
            Chance = chance,
            Target = target
        };

        var id = nextId++;
        AutoSpellSkillsWhenAttacked.Add(id, cast);

        var equipState = new EquipStatChange()
        {
            Slot = (int)activeSlotId,
            Stat = CharacterStat.AutoSpellWhenAttacked,
            Value = (int)skill,
            Change = id,
        };

        equipmentEffects.Add(ref equipState);
    }

    public void GrantSkill(CharacterSkill skill, int level)
    {
        var equipState = new EquipStatChange()
        {
            Slot = (int)activeSlotId,
            Stat = CharacterStat.SkillValue,
            Value = (int)skill,
            Change = level,
        };
        equipmentEffects.Add(ref equipState);
        Player.GrantSkillToCharacter(skill, level);
        CommandBuilder.RefreshGrantedSkills(Player);
    }

    public void AddDamageVsTag(string tag, int change)
    {
        if (!DataManager.TagToIdLookup.TryGetValue(tag, out var tagId))
        {
            ServerLogger.LogWarning($"Item attempted to add bonus damage vs tag {tag}, but no monsters with that tag exists.");
            return;
        }

        var equipState = new EquipStatChange()
        {
            Slot = (int)activeSlotId,
            Change = change,
            Value = tagId,
            Stat = CharacterStat.DamageVsTag
        };

        equipmentEffects.Add(ref equipState);

        if (Player.AttackVersusTag == null)
            Player.AttackVersusTag = new Dictionary<int, int>();

        if (Player.AttackVersusTag.TryGetValue(tagId, out var existing))
            Player.AttackVersusTag[tagId] = existing + change;
        else
            Player.AttackVersusTag.Add(tagId, change);
    }


    public void AddResistVsTag(string tag, int change)
    {
        if (!DataManager.TagToIdLookup.TryGetValue(tag, out var tagId))
        {
            ServerLogger.LogWarning($"Item attempted to add bonus damage vs tag {tag}, but no monsters with that tag exists.");
            return;
        }

        var equipState = new EquipStatChange()
        {
            Slot = (int)activeSlotId,
            Change = change,
            Value = tagId,
            Stat = CharacterStat.ResistVsTag
        };

        equipmentEffects.Add(ref equipState);

        if (Player.ResistVersusTag == null)
            Player.ResistVersusTag = new Dictionary<int, int>();

        if (Player.ResistVersusTag.TryGetValue(tagId, out var existing))
            Player.ResistVersusTag[tagId] = existing + change;
        else
            Player.ResistVersusTag.Add(tagId, change);
    }


    public bool HasLearnedSkill(CharacterSkill skill, int lvl = 1) => Player.MaxLearnedLevelOfSkill(skill) >= lvl;

    public void ChangeWeaponElement(AttackElement element)
    {
        WeaponElement = element;
    }

    public bool IsBaseJob(JobType type) => JobTypes.IsBaseJob(Player.JobId, type);

    public void AddStat(CharacterStat stat, int change)
    {
#if DEBUG
        if (stat >= CharacterStat.Str && stat <= CharacterStat.Luk)
            ServerLogger.LogWarning($"Warning! Adding directly to a base stat {stat} in equip handler for {Player.Inventory?.UniqueItems[(int)activeSlotId]}! You probably want AddStat.");
#endif
        var equipState = new EquipStatChange()
        {
            Slot = (int)activeSlotId,
            Change = change,
            Stat = stat
        };
        equipmentEffects.Add(ref equipState);
        Player.CombatEntity.AddStat(stat, change);

        if (stat == CharacterStat.DoubleAttackChance)
            DoubleAttackModifiers++;
    }

    public void SubStat(CharacterStat stat, int change) => AddStat(stat, -change); //lol

    public void AddStatusEffect(CharacterStatusEffect statusEffect, int duration, int val1 = 0, int val2 = 0)
    {
        var status = StatusEffectState.NewStatusEffect(statusEffect, duration / 1000f, val1, val2);
        Player.CombatEntity.AddStatusEffect(status);
    }

    public void RemoveStatusEffect(CharacterStatusEffect statusEffect)
    {
        Player.CombatEntity.RemoveStatusOfTypeIfExists(statusEffect);
    }

    public void SetPreserveStatusOnDeath(CharacterStatusEffect statusEffect, bool enabled)
    {
        if (enabled)
            Player.CombatEntity.StatusContainer?.SetStatusToKeepOnDeath(statusEffect);
        else
            Player.CombatEntity.StatusContainer?.RemoveStatusFromKeepOnDeath(statusEffect);

    }

    public void SetArmorElement(CharacterElement element)
    {
        if (activeSlotId != (int)EquipSlot.Body)
            ServerLogger.LogWarning(
                $"Warning! Attempting to call SetArmorElement on a card or property in the {activeSlotId} slot. CallStack:\n" +
                Environment.StackTrace);
        else
            ArmorElement = element;
    }

    public int GetExpectedSerializedSize()
    {
        return ItemSlots.Length * 17 + 4; //guids for each inventory slot + 4 for equipped ammo type
    }

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
        bw.Write(AmmoId);
    }

    public void DeSerialize(IBinaryMessageReader br, CharacterBag bag)
    {
        for (var i = 0; i < 10; i++)
        {
            if (br.ReadBoolean())
            {
                var guid = new Guid(br.ReadBytes(16)); //we have a guid, we want to store a bag id
                var bagId = bag.GetUniqueItemByGuid(guid, out var item);

                if (bagId > 0)
                {
                    ItemSlots[i] = bagId;
                    ItemIds[i] = item.Id;
                }

                if (i < 3)
                {
                    var equipInfo = DataManager.ArmorInfo[item.Id];
                    headgearMultiSlotInfo[i] = equipInfo.HeadPosition;
                }

                if (i == (int)EquipSlot.Weapon)
                {
                    var weaponInfo = DataManager.WeaponInfo[item.Id];
                    if (weaponInfo.IsTwoHanded)
                        isTwoHandedWeapon = true;
                }
            }
        }

        EquipAmmo(br.ReadInt32(), false);
    }
}