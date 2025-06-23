using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.UpdateZeny)]
    public class PacketUpdateZeny : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var z = msg.ReadInt32();
            PlayerState.Instance.Zeny = z;
            
            CameraFollower.Instance.CharacterDetailBox.CharacterZeny.text = $"Zeny: {z:N0}";
        }
    }
}