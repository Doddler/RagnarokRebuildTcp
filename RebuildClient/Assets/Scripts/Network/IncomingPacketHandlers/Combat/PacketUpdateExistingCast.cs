using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.UpdateExistingCast)]
    public class PacketUpdateExistingCast : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var addTime = msg.ReadFloat();
            
            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            if (controllable.IsCasting)
                controllable.ExtendCasting(addTime);
        }
    }
}