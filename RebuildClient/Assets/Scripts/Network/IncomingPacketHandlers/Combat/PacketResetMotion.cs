using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.ResetMotion)]
    public class PacketResetMotion : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var srcId = msg.ReadInt32();
            if (Network.EntityList.TryGetValue(srcId, out var controllable))
            {
                if(controllable.SpriteAnimator.State != SpriteState.Dead && controllable.SpriteAnimator.State != SpriteState.Sit)
                    controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Standby, true);
            }
        }
    }
}