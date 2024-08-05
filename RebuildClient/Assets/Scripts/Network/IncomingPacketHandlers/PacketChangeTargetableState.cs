using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers
{
    [ClientPacketHandler(PacketType.ChangeTargetableState)]
    public class PacketChangeTargetableState : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var canTarget = msg.ReadBoolean();

            if (Network.EntityList.TryGetValue(id, out var character))
            {
                character.IsInteractable = canTarget;
                character.FloatingDisplay.HideHpBar();
            }
        }
    }
}