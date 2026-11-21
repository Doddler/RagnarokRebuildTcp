using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Network
{
    [ClientPacketHandler(PacketType.Pong)]
    public class PacketPong : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var sentAt = msg.ReadInt32(); //our own clock, echoed back by the server untouched

            Network.Latency.AddSample(msg.ReceivedAt - sentAt);
        }
    }
}
