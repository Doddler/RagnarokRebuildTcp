using System;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.HandlerBase
{
    public class ClientPacketHandlerAttribute : Attribute
    {
        public readonly PacketType PacketType;

        public ClientPacketHandlerAttribute(PacketType packetType)
        {
            PacketType = packetType;
        }
    }
}