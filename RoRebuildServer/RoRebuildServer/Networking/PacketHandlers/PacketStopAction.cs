using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers;

[ClientPacketHandler(PacketType.StopAction)]
public class PacketStopAction : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
		if (connection.Character == null)
            return;

        var player = connection.Entity.Get<Player>();
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