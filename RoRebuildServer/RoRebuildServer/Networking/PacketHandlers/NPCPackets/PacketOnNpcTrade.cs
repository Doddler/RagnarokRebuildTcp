using System.Diagnostics;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.NPCPackets;

[ClientPacketHandler(PacketType.NpcTradeItem)]
public class PacketOnNpcTrade : IClientPacketHandler
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

        if (!player.IsInNpcInteraction || player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForTrade)
            return;

        var requestedItem = msg.ReadInt32();


        if (requestedItem < 0)
        {
            player.NpcInteractionState.ContinueInteraction();
            return;
        }

        var reqCount = msg.ReadInt32();
        if (reqCount < 0)
        {
            CommandBuilder.ErrorMessage(player, $"Could not complete the trade.");
            ServerLogger.LogWarning($"Could not FinalizeItemTrade as the player requested {requestedItem} items.");
            player.NpcInteractionState.ContinueInteraction();
            return;
        }

        if (reqCount > 99)
        {
            CommandBuilder.ErrorMessage(player, $"The maximum number of items tradeable in one transaction is 99.");
            player.NpcInteractionState.ContinueInteraction();
            return;
        }

        var bagEntryCount = msg.ReadInt32();
        if (bagEntryCount > 10)
            throw new Exception($"Attempting to process PacketOnTrade packet but receiving too many bag IDs");

        Span<int> bagIds = stackalloc int[bagEntryCount];
        for (var i = 0; i < bagEntryCount; i++)
            bagIds[i] = msg.ReadInt32();

        player.NpcInteractionState.FinalizeItemTrade(requestedItem, reqCount, bagIds);

        player.NpcInteractionState.ContinueInteraction();
    }
}