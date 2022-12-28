using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.LookTowards)]
public class PacketLookTowards : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
		if (connection.Character == null)
            return;

        var player = connection.Entity.Get<Player>();
        if (player.InActionCooldown())
        {
            ServerLogger.Debug("Player look action ignored due to cooldown.");
            return;
        }

        if (player.IsInNpcInteraction)
            return;

        player.AddActionDelay(CooldownActionType.FaceDirection);

        var dir = (Direction)msg.ReadByte();
        var head = (HeadFacing)msg.ReadByte();
        connection.Character.ChangeLookDirection(ref connection.Entity, dir, head);
	}
}