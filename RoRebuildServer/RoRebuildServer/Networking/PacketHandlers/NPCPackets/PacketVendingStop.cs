using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;

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
            if (player.VendingState != null)
                player.VendingState.VendProxy = Entity.Null;
        }
        else
        {
            if (player.VendingState != null)
            {
                if (player.VendingState.VendProxy.TryGet<Npc>(out var npc))
                    npc.EndEvent();
                player.VendingState.VendProxy = Entity.Null;
            }
        }

        if (player.NpcInteractionState.InteractionResult == NpcInteractionResult.WaitForVendShop)
        {
            player.EndNpcInteractions();
        }
    }
}