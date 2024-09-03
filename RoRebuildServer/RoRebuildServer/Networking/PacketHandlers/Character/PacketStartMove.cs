using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using System.Diagnostics;

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

        if (!connection.Player.CanPerformCharacterActions())
            return;

        var player = connection.Player;

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
