using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.HitTarget)]
    public class PacketHitTarget : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var damage = msg.ReadInt32();
            var pos = msg.ReadPosition();
            var shouldStop = msg.ReadBoolean();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            controllable.SetHp(Mathf.Max(controllable.Hp - damage, 0), controllable.MaxHp);

            if (shouldStop)
            {
                controllable.StopImmediate(pos, false);
                if (controllable.SpriteAnimator != null)
                {
                    if (controllable.CharacterType != CharacterType.Player || !controllable.SpriteAnimator.IsAttackMotion)
                        controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Hit);
                }
            }
        }
    }
}
