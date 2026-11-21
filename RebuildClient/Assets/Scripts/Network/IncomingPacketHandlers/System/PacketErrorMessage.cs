using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;
using Assets.Scripts.UI.Utility;

namespace Assets.Scripts.Network.IncomingPacketHandlers.System
{
    [ClientPacketHandler(PacketType.ErrorMessage)]
    public class PacketErrorMessage : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var text = msg.ReadString();
            Camera.AppendChatText(text, TextColor.Error);
        }
    }
}