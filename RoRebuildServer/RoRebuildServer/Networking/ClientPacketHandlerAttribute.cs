using RebuildSharedData.Networking;

namespace RoRebuildServer.Networking;

public interface IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg);
}

public class ClientPacketHandlerAttribute : Attribute
{
    public bool VerifyClientConnection;
    public PacketType PacketType;

    public ClientPacketHandlerAttribute(PacketType type, bool verifyClientConnection = true)
    {
        PacketType = type;
        VerifyClientConnection = verifyClientConnection;
    }
}