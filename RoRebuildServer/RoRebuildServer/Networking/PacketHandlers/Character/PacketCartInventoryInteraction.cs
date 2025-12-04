using RebuildSharedData.Networking;
using System.Diagnostics;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Items;
using RebuildSharedData.Enum.EntityStats;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.CartInventoryInteraction)]
public class PacketCartInventoryInteraction : IClientPacketHandler
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

        var id = msg.ReadInt32();
        var count = (int)msg.ReadInt16();
        var cartAction = (CartInteractionType)msg.ReadByte();

        if (player.Inventory == null || count <= 0) return;
        if (!player.DoesCharacterKnowSkill(CharacterSkill.PushCart, 1) || !player.HasCart)
            return;

        switch (cartAction)
        {
            case CartInteractionType.InventoryToCart:
                player.CartInventory ??= CharacterBag.Borrow();
                MoveItemFromInventoryToCart(player, player.Inventory, player.CartInventory, id, count);
                break;
            case CartInteractionType.CartToInventory:
                if (player.CartInventory == null)
                    return;
                MoveItemFromCartToInventory(player, player.Inventory, player.CartInventory, id, count);
                break;
            case CartInteractionType.CartToStorage:
                break;
            case CartInteractionType.StorageToCart:
                break;
        }
    }

    private bool CanFitItemIntoCart(Player player, ItemReference item, CharacterBag cart)
    {
        if (cart.UsedSlots >= 100)
        {
            CommandBuilder.ErrorMessage(player, "You can't fit any more items into your cart.");
            return false;
        }

        var finalWeight = cart.BagWeight + item.Weight * item.Count;
        var capacity = 80000;
        if (finalWeight > capacity)
        {
            CommandBuilder.ErrorMessage(player, $"Insufficient cart weight capacity (over by {(finalWeight - capacity) / 10} weight).");
            return false;
        }

        return true;
    }

    private void MoveItemFromInventoryToCart(Player player, CharacterBag inventory, CharacterBag cart, int bagId, int count)
    {
        if (!inventory.GetItem(bagId, out var item))
        {
            CommandBuilder.ErrorMessage(player, "Unable to find item in inventory.");
            return;
        }

        if (item.Count < count)
        {
            CommandBuilder.ErrorMessage(player, "Unable to move item out of inventory.");
            return;
        }

        if (player.Equipment.IsItemEquipped(bagId))
        {
            CommandBuilder.ErrorMessage(player, "Cannot move an item while it's equipped.");
            return;
        }

        if (item.Type == ItemType.RegularItem)
            item.Count = count;

        if (!CanFitItemIntoCart(player, item, cart))
            return;

        if (!inventory.RemoveItemByBagId(bagId, count))
        {
            CommandBuilder.ErrorMessage(player, "Failed to move item into cart.");
            return;
        }

        CommandBuilder.RemoveItemFromInventory(player, bagId, count);

        var newBagId = cart.AddItem(item);
        if (item.Type == ItemType.RegularItem)
            newBagId = bagId;
        if (cart.GetItem(newBagId, out var newItem))
            CommandBuilder.MoveItemIntoOrOutOfCart(player, CartInteractionType.InventoryToCart, newItem, newBagId, count);
    }

    private void MoveItemFromCartToInventory(Player player, CharacterBag inventory, CharacterBag cart, int bagId, int count)
    {
        if (!cart.GetItem(bagId, out var item))
        {
            CommandBuilder.ErrorMessage(player, "Unable to find item in your cart.");
            return;
        }

        if (item.Count < count)
        {
            CommandBuilder.ErrorMessage(player, "Unable to move item out of your cart.");
            return;
        }


        if (item.Type == ItemType.RegularItem)
            item.Count = count;

        var finalWeight = inventory.BagWeight + item.Weight * item.Count;
        var capacity = player.GetStat(CharacterStat.WeightCapacity);
        if (finalWeight > capacity)
        {
            CommandBuilder.ErrorMessage(player, $"Insufficient weight capacity (over by {(finalWeight - capacity) / 10} weight).");
            return;
        }

        if (!player.CanPickUpItem(item))
        {
            CommandBuilder.ErrorMessage(player, "Insufficient inventory space.");
            return;
        }

        cart.RemoveItemByBagId(bagId, count);
        player.CreateItemInInventory(item);

        CommandBuilder.MoveItemIntoOrOutOfCart(player, CartInteractionType.CartToInventory, item, bagId, count);
    }
}