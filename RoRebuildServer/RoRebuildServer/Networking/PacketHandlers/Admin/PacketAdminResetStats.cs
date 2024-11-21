using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking.PacketHandlers.Admin;

[AdminClientPacketHandler(PacketType.AdminResetStats)]
public class PacketAdminResetStats : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Player == null || !connection.IsConnectedAndInGame || !connection.Player.IsAdmin)
            return;

        connection.Player.StatPointReset();
    }
}