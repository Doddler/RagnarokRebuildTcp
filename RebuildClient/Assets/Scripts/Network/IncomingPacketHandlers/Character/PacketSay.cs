using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.Say)]
    public class PacketSay : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var text = msg.ReadString();
            var name = msg.ReadString();
            var isShout = msg.ReadBoolean();

            if (id == -1)
            {
                Camera.AppendChatText("Server: " + text);
                return;
            }

            if (Network.EntityList.TryGetValue(id, out var controllable))
            {
                if (isShout)
                {
                    controllable.DialogBox($"{name}: <i><color=#FFB051>{text}</color></i>");
                    Camera.AppendChatText($"{name} shouts: <i><color=#FFB051>{text}</color></i>");
                }
                else
                {
                    controllable.DialogBox($"{name}: {text}");
                    Camera.AppendChatText($"{name}: {text}");
                }
            }
            else
            {
                if (isShout)
                    Camera.AppendChatText($"{name} shouts: <i><color=#FFB051>{text}</color></i>");
                else
                    Camera.AppendChatText($"{name} nearby: <i><color=#FFFF6A>{text}</color></i>");
            }
        }
    }
}