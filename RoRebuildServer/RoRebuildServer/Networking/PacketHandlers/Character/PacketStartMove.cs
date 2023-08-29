using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.StartMove)]
public class PacketStartMove : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null)
            return; //we don't accept the keep-alive packet if they haven't entered the world yet

        var player = connection.Entity.Get<Player>();
        if (player.InActionCooldown())
        {
            ServerLogger.Debug("Player click ignored due to cooldown.");
            return;
        }

        if (player.IsInNpcInteraction)
            return;

        player.AddActionDelay(CooldownActionType.Click);

        //player.Target = EcsEntity.Null;

        var x = msg.ReadInt16();
        var y = msg.ReadInt16();

        var target = new Position(x, y);

        //if (connection.Character.TryMove(ref connection.Entity, target, 0))
        connection.Character.TryMove(target, 0);
        player.ClearTarget();
    }
}
