using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.UpdateZeny)]
    public class PacketUpdateZeny : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var z = msg.ReadInt32();
            
            CameraFollower.Instance.CharacterDetailBox.CharacterZeny.text = $"Zeny: {z:N0}";
        }
    }
}