using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Util;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.StopAction)]
public class PacketStopAction : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
		if (connection.Character == null || connection.Player == null)
            return;

        var player = connection.Player;

        if (!player.CanPerformCharacterActions(true))
            return;

        player.AddInputActionDelay(InputActionCooldownType.StopAction);

        connection.Character.ShortenMovePath();

        connection.Player.ClearTarget();
	}
}