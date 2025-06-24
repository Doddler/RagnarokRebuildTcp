using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using System.Diagnostics;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.LookTowards)]
public class PacketLookTowards : IClientPacketHandler
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

        player.AddInputActionDelay(InputActionCooldownType.FaceDirection);

        var dir = (Direction)msg.ReadByte();
        var head = (HeadFacing)msg.ReadByte();
        connection.Character.ChangeLookDirection(player.Character.Position.AddDirectionToPosition(dir), head);
	}
}