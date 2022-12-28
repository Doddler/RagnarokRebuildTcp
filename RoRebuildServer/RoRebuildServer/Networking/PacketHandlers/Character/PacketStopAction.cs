using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.StopAction)]
public class PacketStopAction : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
		if (connection.Character == null || connection.Player == null)
            return;

        var player = connection.Player;

        if (player.InActionCooldown())
        {
            ServerLogger.Debug("Player stop action ignored due to cooldown.");
            return;
        }

        if (player.IsInNpcInteraction)
            return;

        player.AddActionDelay(CooldownActionType.StopAction);

        connection.Character.StopAction();

        connection.Player.ClearTarget();
	}
}