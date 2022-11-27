using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking.PacketHandlers.NPC;

[ClientPacketHandler(PacketType.NpcSelectOption)]
public class PacketNpcOption : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var player = connection.Player;

        if (player is not { IsInNpcInteraction: true }
            || player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForInput)
            return;

        var result = msg.ReadInt32();

        player.NpcInteractionState.OptionInteraction(result);
    }
}