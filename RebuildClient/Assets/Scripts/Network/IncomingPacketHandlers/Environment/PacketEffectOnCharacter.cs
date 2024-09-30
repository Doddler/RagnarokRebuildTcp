using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Environment
{
    [ClientPacketHandler(PacketType.EffectOnCharacter)]
    public class PacketEffectOnCharacter : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var effect = msg.ReadInt32();


            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            Camera.AttachEffectToEntity(effect, controllable.gameObject, controllable.Id);
        }
    }
}