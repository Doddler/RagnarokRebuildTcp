using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.StopCast)]
    public class PacketStopCasting : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            if (Network.EntityList.TryGetValue(id, out var controllable))
            {
                controllable.StopCasting();
            }
        }
    }
}