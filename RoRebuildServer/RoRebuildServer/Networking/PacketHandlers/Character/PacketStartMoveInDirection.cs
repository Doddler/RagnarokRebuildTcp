using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Util;
using System.Diagnostics;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.StartWalkInDirection)]
public class PacketStartMoveInDirection : IClientPacketHandler
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

        var x = msg.ReadInt16();
        var y = msg.ReadInt16();

        var targetDirection = new Position(x, y);

        // if character cannot move from command in new direction, stop any existing moves
        if (!connection.Character.TryMoveInDirection(targetDirection))
            connection.Character.StopMovingImmediately();

        player.ClearTarget();
    }
}
