using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Data;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Environment
{
    [ClientPacketHandler(PacketType.MemoMapLocation)]
    public class PacketMemoMapLocation : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            for(var i = 0; i < 4; i++)
                State.MemoLocations[i] = MapMemoLocation.DeSerialize(msg);
        }
    }
}