using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.System
{
    [ClientPacketHandler(PacketType.ErrorMessage)]
    public class PacketErrorMessage : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var text = msg.ReadString();
            Camera.AppendChatText($"<color=#FF7777>{text}</color>");
        }
    }
}