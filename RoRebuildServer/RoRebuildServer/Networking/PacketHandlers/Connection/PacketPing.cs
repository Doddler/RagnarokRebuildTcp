using RebuildSharedData.Networking;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Networking.PacketHandlers.Connection;

[ClientPacketHandler(PacketType.Ping)]
public class PacketPing : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character == null || !connection.Character.IsActive)
        {
            ServerLogger.Debug("Ignored player ping packet as the player isn't alive in the world yet.");
            return; //we don't accept the keep-alive packet if they haven't entered the world yet
        }

        connection.LastKeepAlive = Time.ElapsedTime;
    }
}