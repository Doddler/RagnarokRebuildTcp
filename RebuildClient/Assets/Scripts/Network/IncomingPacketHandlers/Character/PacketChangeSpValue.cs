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
            
            if(CameraFollower.Instance.TargetControllable != null)
                CameraFollower.Instance.TargetControllable.SetSp(sp, maxSp);
            CameraFollower.Instance.UpdatePlayerSP(sp, maxSp);
        }
    }
}