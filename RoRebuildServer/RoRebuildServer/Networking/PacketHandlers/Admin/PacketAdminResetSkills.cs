using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking.PacketHandlers.Admin
{
    [AdminClientPacketHandler(PacketType.AdminResetSkills)]
    public class PacketAdminResetSkills : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (connection.Player == null || !connection.IsConnectedAndInGame || !connection.Player.IsAdmin)
                return;

            connection.Player.LearnedSkills.Clear();
            connection.Player.UpdateStats();
        }
    }
}
