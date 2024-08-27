using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Sprites;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.Emote)]
    public class PacketEmote : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var emote = msg.ReadInt32();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            ClientDataLoader.Instance.AttachEmote(controllable.gameObject, emote);
        }
    }
}