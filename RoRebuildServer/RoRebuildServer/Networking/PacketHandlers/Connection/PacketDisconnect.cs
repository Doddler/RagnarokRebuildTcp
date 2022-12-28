using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking.PacketHandlers.Connection;

[ClientPacketHandler(PacketType.Disconnect, false)]
public class PacketDisconnect : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        NetworkManager.QueueDisconnect(connection);
    }
}