using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Networking;
using Wintellect.PowerCollections;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RoRebuildServer.EntityComponents.Character;

public enum RefineSuccessResult
{
    Success,
    FailedWithLevelDown,
    FailedNoChange,
    FailedIncorrectRequirements,
    FailedCatalystMismatch
}

public static class EquipmentRefineSystem
{
    private static int[] refineCosts = [200, 1000, 5000, 20000, 2000];
    private static int[] itemForUpgrade = [1010, 1011, 984, 984, 985];
    private static int catalystBonus = 10;

    public static RefineSuccessResult AttemptRefineItem(Player player, ref ItemReference item, int ore, int catalyst)
    {
        var inventory = player.Inventory;
        var data = DataManager.GetItemInfoById(item.Id);
        if (inventory == null || data == null || item.Type != ItemType.UniqueItem || item.UniqueItem.Refine >= 10)
            return RefineSuccessResult.FailedIncorrectRequirements;

        var rank = 4;
        if (data.ItemClass == ItemClass.Weapon)
        {
            var weapon = DataManager.WeaponInfo[item.Id];
            if (!weapon.IsRefinable)
                return RefineSuccessResult.FailedIncorrectRequirements;
            rank = DataManager.WeaponInfo[item.Id].WeaponLevel - 1;
        }
        else
        {
            var armor = DataManager.ArmorInfo[item.Id];
            if(!armor.IsRefinable)
                return RefineSuccessResult.FailedIncorrectRequirements;
        }

        if (rank < 0 || rank > itemForUpgrade.Length)
            return RefineSuccessResult.FailedIncorrectRequirements;

        var requiredOre = itemForUpgrade[rank];
        var requiredZeny = refineCosts[rank];

        if (ore != requiredOre || player.GetZeny() < requiredZeny || !player.TryRemoveItemFromInventory(requiredOre, 1, true))
            return RefineSuccessResult.FailedIncorrectRequirements;

        var successRate = DataManager.GetRefineSuccessForItem(rank, item.UniqueItem.Refine);
        var forceSafe = false;

        if (catalyst > 0)
        {
            inventory.GetItem(catalyst, out var catalystItem);
            if (catalystItem.Id != item.Id || catalystItem.Type != ItemType.UniqueItem || catalystItem.UniqueItem.Refine > 0)
                return RefineSuccessResult.FailedCatalystMismatch;
            for(var i = 0; i < 4; i++)
                if (catalystItem.UniqueItem.SlotData(i) > 0)
                    return RefineSuccessResult.FailedCatalystMismatch;

            if (!inventory.RemoveUniqueItem(catalyst))
                return RefineSuccessResult.FailedCatalystMismatch;

            CommandBuilder.RemoveItemFromInventory(player, catalyst, 1, true);

            successRate += catalystBonus;
            forceSafe = true;
        }

        player.DropZeny(requiredZeny);
        
        if (successRate < 100)
        {
            if (GameRandom.Next(0, 100) > successRate)
            {
                if (forceSafe)
                    return RefineSuccessResult.FailedNoChange;

                item.UniqueItem.Refine -= 1;
                return RefineSuccessResult.FailedWithLevelDown;
            }
        }

        item.UniqueItem.Refine += 1;
        return RefineSuccessResult.Success;
    }

    public static bool AdminItemUpgrade(Player player, int bagId, int upgradeLevel)
    {
        if (player.Inventory == null || !player.Inventory.GetItem(bagId, out var targetItem) ||
            targetItem.Type != ItemType.UniqueItem)
            return false;

        var data = DataManager.GetItemInfoById(targetItem.Id);
        if (data == null) return false;
        if (data.ItemClass == ItemClass.Weapon)
        {
            var weapon = DataManager.WeaponInfo[targetItem.Id];
            if (!weapon.IsRefinable)
            {
                CommandBuilder.ErrorMessage(player, "Item is unrefineable.");
                return false;
            }
        }
        else
        {
            var armor = DataManager.ArmorInfo[targetItem.Id];
            if (!armor.IsRefinable)
            {
                CommandBuilder.ErrorMessage(player, "Item is unrefineable.");
                return false;
            }
        }

        var isEquipped = player.Equipment.IsItemEquipped(bagId);
        if (isEquipped)
            player.Equipment.UnEquipItem(bagId);

        var recipients = player.Character.GetVisiblePlayerList();

        CommandBuilder.AddRecipients(recipients);
        CommandBuilder.SendEffectOnCharacterMulti(player.Character, DataManager.EffectIdForName["RefineSuccess"]);
        CommandBuilder.ClearRecipients();

        targetItem.UniqueItem.Refine = byte.Clamp((byte)upgradeLevel, 0, 10);

        player.Inventory.UpdateUniqueItemReference(bagId, targetItem.UniqueItem);
        CommandBuilder.PlayerUpdateInventoryItemState(player, bagId, targetItem.UniqueItem);
        if (isEquipped)
        {
            player.Equipment.EquipItem(bagId);
            player.UpdateStats(false);
        }

        return true;
    }
}