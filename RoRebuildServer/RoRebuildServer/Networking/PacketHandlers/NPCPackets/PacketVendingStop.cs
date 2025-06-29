using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Networking.PacketHandlers.NPCPackets;

[ClientPacketHandler(PacketType.VendingStop)]
public class PacketVendingStop : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var player = connection.Player;

        if (player == null || !player.IsInNpcInteraction)
            return;

        if (player.NpcInteractionState.InteractionResult == NpcInteractionResult.CurrentlyVending)
        {
            if (player.NpcInteractionState.NpcEntity.TryGet<Npc>(out var npc))
                npc.EndEvent();

            player.EndNpcInteractions();
        }

        if (player.NpcInteractionState.InteractionResult == NpcInteractionResult.WaitForVendShop)
        {
            player.EndNpcInteractions();
        }
    }
}