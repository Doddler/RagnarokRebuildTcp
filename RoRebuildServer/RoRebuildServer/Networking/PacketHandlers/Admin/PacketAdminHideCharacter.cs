using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking.PacketHandlers.Admin
{
    [AdminClientPacketHandler(PacketType.AdminHideCharacter)]
    public class PacketAdminHideCharacter : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (!connection.IsOnlineAdmin)
                return;

            var desiredStatus = msg.ReadBoolean();

            connection.Character!.AdminHidden = desiredStatus;

            CommandBuilder.SendAdminHideStatus(connection.Player!, desiredStatus);
        }
    }
}
