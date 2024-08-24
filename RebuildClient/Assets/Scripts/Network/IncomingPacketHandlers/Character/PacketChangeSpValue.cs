using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.ChangeSpValue)]
    public class PacketChangeSpValue : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var sp = msg.ReadInt32();
            var maxSp = msg.ReadInt32();
            
            CameraFollower.Instance.UpdatePlayerSP(sp, maxSp);
        }
    }
}