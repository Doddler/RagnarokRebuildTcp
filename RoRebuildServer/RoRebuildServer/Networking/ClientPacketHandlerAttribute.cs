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
    public bool IsAdminPacket;

    public ClientPacketHandlerAttribute(PacketType type, bool verifyClientConnection = true, bool isAdminPacket = false)
    {
        PacketType = type;
        VerifyClientConnection = verifyClientConnection;
        IsAdminPacket = isAdminPacket;
    }
}

public class AdminClientPacketHandlerAttribute : ClientPacketHandlerAttribute
{
    public AdminClientPacketHandlerAttribute(PacketType type, bool verifyClientConnection = true) : base(type, verifyClientConnection, true) { }
}