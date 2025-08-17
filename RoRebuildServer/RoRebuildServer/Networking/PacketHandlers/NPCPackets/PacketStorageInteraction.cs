using System.Diagnostics;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Items;

namespace RoRebuildServer.Networking.PacketHandlers.NPCPackets;

[ClientPacketHandler(PacketType.StorageInteraction)]
public class PacketStorageInteraction : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsPlayerAlive)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);
        
        var player = connection.Player;
        var map = connection.Character.Map;

        if (!player.IsInNpcInteraction || player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForStorage)
            return;


        var itemMoveType = msg.ReadByte();

        if (itemMoveType == 0)
        {
            player.EndNpcInteractions();
            return;
        }

        var bagId = msg.ReadInt32();
        var count = msg.ReadInt32();

        if (count <= 0)
            return;

        //inventory to storage
        if (itemMoveType == 1)
        {
            if (player.Inventory == null)
                return;

            player.StorageInventory ??= CharacterBag.Borrow();
            if (player.StorageInventory.UsedSlots >= 600)
            {
                CommandBuilder.ErrorMessage(player, "Storage full, cannot add more items.");
                return;
            }

            if (player.Equipment.IsItemEquipped(bagId))
            {
                CommandBuilder.ErrorMessage(player, "Cannot store an item while it's equipped.");
                return;
            }

            if (!player.Inventory.RemoveItemByBagIdAndGetRemovedItem(bagId, count, out var item))
            {
                CommandBuilder.ErrorMessage(player, "Failed to move item into storage.");
                return;
            }
            CommandBuilder.RemoveItemFromInventory(player, bagId, count);

            var newBagId = item.Id;
            
            if (item.Type == ItemType.RegularItem)
                player.StorageInventory.AddItem(item);
            else
                newBagId = player.StorageInventory.AddItem(item);

            if (player.StorageInventory.GetItem(newBagId, out var newStack))
                CommandBuilder.SendNpcStorageMoveEvent(player, newStack, newBagId, count, true);
        }

        //storage to inventory
        if (itemMoveType == 2)
        {
            if (player.StorageInventory == null)
                return;

            player.Inventory ??= CharacterBag.Borrow();
            
            if(!player.StorageInventory.GetItem(bagId, out var item))
            {
                CommandBuilder.ErrorMessage(player, "Unable to find item in storage. If this persists try exiting and reloading storage.");
                return;
            }

            if (item.Count < count)
            {
                CommandBuilder.ErrorMessage(player, "Unable to move item out of storage. If this persists try exiting and reloading storage.");
                return;
            }

            if (item.Type == ItemType.RegularItem)
                item.Count = count;

            var finalWeight = player.Inventory.BagWeight + item.Weight * item.Count;
            var capacity = player.GetStat(CharacterStat.WeightCapacity);
            if (finalWeight > capacity)
            {
                CommandBuilder.ErrorMessage(player, $"Insufficient weight capacity (over by {(finalWeight - capacity)/10} weight).");
                return;
            }
                
            if (!player.CanPickUpItem(item))
            {
                CommandBuilder.ErrorMessage(player, "Insufficient inventory space.");
                return;
            }

            player.StorageInventory.RemoveItemByBagId(bagId, count);
            player.CreateItemInInventory(item);

            CommandBuilder.SendNpcStorageMoveEvent(player, item, bagId, count, false);
        }
    }
}