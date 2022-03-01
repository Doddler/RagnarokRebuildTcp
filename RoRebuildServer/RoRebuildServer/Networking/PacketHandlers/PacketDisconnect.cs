using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking.PacketHandlers
{
    [ClientPacketHandler(PacketType.Disconnect, false)]
    public class PacketDisconnect : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            NetworkManager.DisconnectPlayer(connection);
        }
    }
}
