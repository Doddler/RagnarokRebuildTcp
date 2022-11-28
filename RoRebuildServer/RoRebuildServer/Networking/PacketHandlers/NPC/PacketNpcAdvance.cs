using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.NPC;

[ClientPacketHandler(PacketType.NpcAdvance)]
public class PacketNpcAdvance : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var player = connection.Player;

        if(player is not { IsInNpcInteraction: true } 
           || player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForContinue)
            return;

        if (player.InActionCooldown())
            return;
        player.AddActionDelay(CooldownActionType.Click);

        player.NpcInteractionState.ContinueInteraction();
    }
}