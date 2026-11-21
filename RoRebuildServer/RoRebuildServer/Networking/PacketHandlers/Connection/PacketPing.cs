using RebuildSharedData.Networking;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Networking.PacketHandlers.Connection;

[ClientPacketHandler(PacketType.Ping)]
public class PacketPing : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        //Echoed back untouched below, so the client can work out its own round trip and we track nothing per connection.
        var clientTimestamp = msg.ReadInt32();

        //If they haven't selected a character we accept keep alive packets for 20 minutes
        //If they have, we'll ignore keep alive packets if they aren't active in game yet.
        //Effectively that means they have about a minute to load the map before we time them out.

        if (connection.Character == null)
        {
            if (connection.LoginTime + 1200 < Time.ElapsedTimeFloat)
            {
                //we'll let them keep the connection alive during character select for 20 minutes
                ServerLogger.Debug("Ignored player ping packet as the player has been idle on character select for 20 minutes.");
                return; //we don't accept the keep-alive packet if they haven't entered the world yet
            }
        }
        else
        {
            if (!connection.Character.IsActive)
            {
                ServerLogger.Debug("Ignored player ping packet as the player isn't alive in the world yet.");
                return; //we don't accept the keep-alive packet if they haven't entered the world yet
            }
        }

        connection.LastKeepAlive = Time.ElapsedTime;

        //On character select the ping stays a pure keep-alive, with no session for a latency reading to describe.
        if (connection.Character == null)
            return;

        var packet = NetworkManager.StartPacket(PacketType.Pong, 8);
        packet.Write(clientTimestamp);
        NetworkManager.SendMessage(packet, connection);
    }
}