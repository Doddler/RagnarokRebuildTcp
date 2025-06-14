using RebuildSharedData.Networking;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.SocketEquipment)]
public class PacketSocketEquipment : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsPlayerAlive)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        if (!connection.Player.CanPerformCharacterActions())
            return;

        var player = connection.Player;
        var inventory = player.Inventory;
        if (inventory == null)
            return;

        var targetBagId = msg.ReadInt32();
        var srcBagId = msg.ReadInt32();

        if (!inventory.GetItem(targetBagId, out var targetItem) || targetItem.Type != ItemType.UniqueItem) //get the target item from our inventory
        {
            ServerLogger.LogWarning($"Player {player} tried to socket item id {srcBagId} into an item (bagId {targetBagId}), but the target item could not be found.");
            goto OnError;
        }

        if (((UniqueItemFlags)targetItem.UniqueItem.Flags & UniqueItemFlags.CraftedItem) > 0)
        {
            CommandBuilder.ErrorMessage(connection, "You can't slot anything onto a crafted item.");
            return;
        }

        var targetData = DataManager.GetItemInfoById(targetItem.Id); //get details on the target item
        if (targetData == null)
        {
            ServerLogger.LogWarning($"Could not find item info for id {targetItem.Id} for socketing.");
            goto OnError;
        }

        if (!inventory.GetItem(srcBagId, out var socketingItem)) //get the socketing item from our inventory
        {
            ServerLogger.LogWarning($"Player {player} tried to socket item id {srcBagId} into an item {targetItem.Id}, but they don't have the socketing item.");
            goto OnError;
        }

        var srcData = DataManager.GetItemInfoById(socketingItem.Id); //get the socketing item details
        if (srcData == null)
        {
            ServerLogger.LogWarning($"Could not find item info for id {targetItem.Id} for socketing.");
            goto OnError;
        }

        var maxSlots = 0;
        var targetPos = EquipPosition.Weapon;
        if (targetData.ItemClass == ItemClass.Equipment)
        {
            var armor = DataManager.ArmorInfo[targetData.Id];
            maxSlots = armor.CardSlots;
            targetPos = armor.EquipPosition;
        }

        if (targetData.ItemClass == ItemClass.Weapon)
            maxSlots = DataManager.WeaponInfo[targetData.Id].CardSlots;

        if (srcData.ItemClass != ItemClass.Card || maxSlots <= 0) //make sure it's possible to socket
        {
            ServerLogger.LogWarning($"Player {player} tried to socket item {srcData.Code} into an item {targetData.Code}, but it is an invalid socket request.");
            goto OnError;
        }

        var srcPos = DataManager.CardInfo[srcData.Id].EquipPosition;
        if ((srcPos & targetPos) == 0)
        {
            ServerLogger.LogWarning($"Player {player} tried to socket item {srcData.Code} into an item {targetData.Code}, but they don't match in socket type.");
            CommandBuilder.ErrorMessage(connection, $"Could not perform socketing, the socket types are incompatible.");
            return;
        }

        var targetSlot = -1; //find what slot we want to socket this item into
        for (var i = 0; i < maxSlots; i++)
        {
            if (targetItem.UniqueItem.SlotData(i) == 0)
            {
                targetSlot = i;
                break;
            }
        }
            
        if (targetSlot < 0) // if there are no free slots
        {
            ServerLogger.LogWarning($"Player {player} tried to socket item {srcData.Code} into an item {targetData.Code}, but it has no free slots.");
            goto OnError;
        }

        if (!player.TryRemoveItemFromInventory(srcData.Id, 1))
        {
            ServerLogger.LogWarning($"Player {player} tried to socket item {srcData.Code} into an item {targetData.Code}, but the item could not be removed from their inventory.");
            goto OnError;
        }

        var item = targetItem.UniqueItem;
        item.SetSlotData(targetSlot, srcData.Id);
        CommandBuilder.RemoveItemFromInventory(player, srcBagId, 1);
        CommandBuilder.PlayerUpdateInventoryItemState(player, targetBagId, item);
        inventory.UpdateUniqueItemReference(targetBagId, item);
        var occupiedSlot = player.Equipment.GetOccupiedSlotForItem(targetBagId);

        if (occupiedSlot != EquipSlot.None) //if the item that is being socketed is already equipped, run the equip handler for the new card
        {
            player.Equipment.PerformOnEquipForNewCard(srcData, occupiedSlot);
            player.UpdateStats(false);
        }

        player.WriteCharacterToDatabase();


        return;

        OnError:
        CommandBuilder.ErrorMessage(connection, "Socketing failed.");

    }
}