using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Util;
using System.Diagnostics;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.StartWalk)]
public class PacketStartMove : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsPlayerAlive)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        var player = connection.Player;

        if (!player.CanPerformCharacterActions())
            return;

        if (player.CombatEntity.HasBodyState(BodyStateFlags.Hidden))
            return;

        player.AddInputActionDelay(InputActionCooldownType.Click);

        //player.Target = EcsEntity.Null;

        var x = msg.ReadInt16();
        var y = msg.ReadInt16();

        var target = new Position(x, y);

        //if (connection.Character.TryMove(ref connection.Entity, target, 0))
        connection.Character.TryMove(target, 0);
        player.ClearTarget();
    }
}
