using System.Buffers;
using System.Diagnostics;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Items;

namespace RoRebuildServer.Networking.PacketHandlers.NPCPackets;

[ClientPacketHandler(PacketType.ShopBuySell)]
public class PacketShopBuySell : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsPlayerAlive)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        //if (!connection.Player.CanPerformCharacterActions())
        //    return;

        var player = connection.Player;
        var map = connection.Character.Map;

        if (!player.IsInNpcInteraction || player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForShop)
            return;

        var isBuying = player.NpcInteractionState.IsBuyingFromNpc;

        var count = msg.ReadInt32();

        if ((isBuying && count > 20) || (!isBuying && count > CharacterBag.MaxBagSlots))
            throw new Exception($"Player {player} is attempting to buy/sell (isBuying:{isBuying}) {count} items, which is over the limit.");

        if (count == 0)
        {
            player.NpcInteractionState.ContinueInteraction();
            return;
        }

        var itemIds = ArrayPool<int>.Shared.Rent(count);
        var itemCounts = ArrayPool<int>.Shared.Rent(count);

        for (var i = 0; i < count; i++)
        {
            itemIds[i] = msg.ReadInt32();
            itemCounts[i] = msg.ReadInt32();
            if (itemCounts[i] <= 0)
                return; //no
        }

        var npc = player.NpcInteractionState.NpcEntity.Get<Npc>();

        if (isBuying)
            npc.SubmitPlayerPurchaseFromNpc(player, itemIds, itemCounts, count, player.NpcInteractionState.AllowDiscount);
        else
            npc.SubmitPlayerSellItemsToNpc(player, itemIds, itemCounts, count);

        ArrayPool<int>.Shared.Return(itemIds);
        ArrayPool<int>.Shared.Return(itemCounts);

        player.NpcInteractionState.ContinueInteraction();
    }
}