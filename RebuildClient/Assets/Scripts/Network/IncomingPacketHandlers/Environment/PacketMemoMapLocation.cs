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
            var locations = new MapMemoLocation[State.MemoLocations.Count];
            for (var i = 0; i < locations.Length; i++)
                locations[i] = MapMemoLocation.DeSerialize(msg);
            State.SetMemoLocations(locations);
        }
    }
}