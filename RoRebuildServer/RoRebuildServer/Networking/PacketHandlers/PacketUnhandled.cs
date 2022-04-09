using RebuildSharedData.Networking;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers;

[ClientPacketHandler(PacketType.UnhandledPacket, false)]
public class PacketUnhandled : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if(connection.Player != null)
            ServerLogger.LogWarning($"Received unhandled packet type {NetworkManager.LastPacketType} for player '{connection.Player.Name}'. Player will be disconnected.");
        else
            ServerLogger.LogWarning($"Received unhandled packet type {NetworkManager.LastPacketType}. Player will be disconnected.");

        NetworkManager.PacketHandlers[(int)PacketType.Disconnect](connection, msg);
    }
}