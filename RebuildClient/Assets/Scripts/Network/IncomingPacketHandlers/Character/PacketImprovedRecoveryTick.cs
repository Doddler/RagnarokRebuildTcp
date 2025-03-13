using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.ImprovedRecoveryTick)]
    public class PacketImprovedRecoveryTick : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var charId = msg.ReadInt32();
            var hpGain = (int)msg.ReadInt16();
            var spGain = (int)msg.ReadInt16();

            if (!Network.EntityList.TryGetValue(charId, out var controllable))
                return;

            if (hpGain > 0)
            {
                RecoveryParticlesEffect.LaunchRecoveryParticles(controllable, true);
                controllable.AttachHealIndicator(hpGain);
            }

            if (spGain > 0)
            {
                RecoveryParticlesEffect.LaunchRecoveryParticles(controllable, false);
                controllable.AttachHealSpIndicator(spGain);
            }
        }
    }
}