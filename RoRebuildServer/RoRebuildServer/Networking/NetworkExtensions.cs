using RebuildSharedData.Data;
using RebuildZoneServer.Networking;

namespace RoRebuildServer.Networking;

public static class NetworkExtensions
{
    public static void Write(this OutboundMessage buffer, Position position)
    {
        buffer.Write((short)position.X);
        buffer.Write((short)position.Y);
    }
}