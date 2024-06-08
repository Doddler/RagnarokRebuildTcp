using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation;
using System;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.SitStand)]
public class PacketSitStand : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
		if (connection.Character == null)
            return;

        var player = connection.Entity.Get<Player>();
        if (player.InActionCooldown())
        {
            ServerLogger.Debug("Player sit/stand action ignored due to cooldown.");
            return;
        }

        if (player.IsInNpcInteraction)
            return;

        player.AddActionDelay(CooldownActionType.SitStand);

        var isSitting = msg.ReadBoolean();
        connection.Character.SitStand(isSitting);
	}
}