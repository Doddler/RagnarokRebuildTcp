using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Util;

namespace RoRebuildServer.Networking.PacketHandlers.NPCPackets;

[ClientPacketHandler(PacketType.NpcAdvance)]
public class PacketNpcAdvance : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var player = connection.Player;

        if (player == null || !player.IsInNpcInteraction)
            return;

        if (player.NpcInteractionState.InteractionResult == NpcInteractionResult.CurrentlyVending)
        {
            player.NpcInteractionState.StopVending();
            return;
        }

        if (player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForContinue &&
            player.NpcInteractionState.InteractionResult != NpcInteractionResult.WaitForRefine)
            return;

        if (player.InInputActionCooldown())
            return;
        player.AddInputActionDelay(InputActionCooldownType.Click);

        player.NpcInteractionState.ContinueInteraction();
        if (player.NpcInteractionState.InteractionResult == NpcInteractionResult.WaitForRefine)
            player.NpcInteractionState.OptionResult = -1;
    }
}