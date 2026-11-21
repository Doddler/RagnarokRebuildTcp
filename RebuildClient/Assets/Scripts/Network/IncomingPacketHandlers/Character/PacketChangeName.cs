using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.ChangeName)]
    public class PacketChangeName : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var text = msg.ReadString();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            Camera.AppendChatText($"{controllable.Name} has changed their name to {text}.");
            controllable.SetName(text);
        }
    }
}
