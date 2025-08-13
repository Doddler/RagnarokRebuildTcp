using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using System.Diagnostics;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Items;
using RebuildSharedData.Enum.EntityStats;

namespace RoRebuildServer.Networking.PacketHandlers.NPCPackets;

[ClientPacketHandler(PacketType.VendingPurchaseFromStore)]
public class PacketVendingPurchaseFromStore : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsPlayerAlive)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        var player = connection.Player;
        //var map = connection.Character.Map;
            
        if (!player.IsInNpcInteraction || player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForVendShop)
            return;

        if (!player.NpcInteractionState.NpcEntity.TryGet<Npc>(out var npc) 
            || !npc.Owner.TryGet<Player>(out var vendor)
            || !vendor.IsInNpcInteraction
            || !vendor.HasCart
            || vendor.NpcInteractionState.InteractionResult != NpcInteractionResult.CurrentlyVending 
            || vendor.VendingState == null
            || vendor.CartInventory == null)
        {
            CommandBuilder.ErrorMessage(player, "Could not complete transaction, store is no longer available.");
            player.NpcInteractionState.ContinueInteraction();
            return;
        }
            
        player.NpcInteractionState.ContinueInteraction(); //get this out of the way, every result will end with the buy window closing

        var purchaseCount = msg.ReadInt32();
        if (purchaseCount == 0)
            return;

        player.Inventory ??= CharacterBag.Borrow(); //somehow they might have no primary bag and try to buy an item... it should be impossible but why not check?

        if (player.Inventory.UsedSlots + purchaseCount > CharacterBag.MaxBagSlots)
        {
            CommandBuilder.ErrorMessage(player, "You have too many items in your inventory to complete the transaction.");
            return;
        }

        Span<int> bagIds = stackalloc int[purchaseCount];
        Span<int> itemCounts = stackalloc int[purchaseCount];
        Span<int> curShopCounts = stackalloc int[purchaseCount];
        var shop = vendor.VendingState;
        var cart = vendor.CartInventory;
        var inventory = player.Inventory;
        var totalCost = 0;
        var totalWeight = 0;

        for (var i = 0; i < purchaseCount; i++)
        {
            var bagId = msg.ReadInt32();
            var count = msg.ReadInt32();

            if (count <= 0)
                return; //no

            if (!shop.SellingItems.TryGetValue(bagId, out var item) || item.Count < count || cart.GetItemCountByBagId(bagId) < count)
            {
                CommandBuilder.ErrorMessage(player, "Could not complete purchase, one or more items are no longer available.");
                return;
            }

            totalCost += shop.SellingItemValues[bagId] * count;
            totalWeight = item.Weight * count;

            bagIds[i] = bagId;
            itemCounts[i] = count;
            curShopCounts[i] = item.Count;
        }

        var existingWeight = inventory?.BagWeight ?? 0;
        if (existingWeight + totalWeight > player.GetStat(CharacterStat.WeightCapacity))
        {
            CommandBuilder.ErrorMessage(player, $"Could not complete purchase, you can't carry that much weight.");
            return;
        }

        var zeny = player.GetData(PlayerStat.Zeny);
        if (totalCost > zeny)
        {
            CommandBuilder.ErrorMessage(player, $"Could not complete purchase, you don't have enough zeny.");
            return;
        }

        //we got this far, lets go
        player.SetData(PlayerStat.Zeny, zeny - totalCost); //do this first
        for (var i = 0; i < purchaseCount; i++)
        {
            cart.RemoveItemByBagIdAndGetRemovedItem(bagIds[i], itemCounts[i], out var item);

            //put the item in the player bag
            player.CreateItemInInventory(item);

            //remove the item from the vendor's shop
            if (curShopCounts[i] > itemCounts[i])
            {
                var saleItem = shop.SellingItems[bagIds[i]];
                saleItem.Count -= itemCounts[i];
                shop.SellingItems[bagIds[i]] = saleItem;
            }
            else
            {
                shop.SellingItemValues.Remove(bagIds[i]);
                shop.SellingItems.Remove(bagIds[i]);
            }

            CommandBuilder.VendingNotifyOfSale(vendor, bagIds[i], itemCounts[i]);
        }
        vendor.SetData(PlayerStat.Zeny, vendor.GetData(PlayerStat.Zeny) + totalCost); //do this last

        CommandBuilder.SendServerEvent(player, ServerEvent.TradeSuccess, -totalCost);

        player.WriteCharacterToDatabase(); //save immediately, no shenanigans
        CommandBuilder.SendUpdatePlayerData(player, true, false);

        vendor.WriteCharacterToDatabase(); //save immediately
        //CommandBuilder.SendUpdatePlayerData(vendor, false, false);

        if (shop.SellingItemValues.Count == 0)
        {
            npc.EndEvent();

            CommandBuilder.VendingEnd(vendor);
            vendor.NpcInteractionState.CancelInteraction();
        }
    }
}