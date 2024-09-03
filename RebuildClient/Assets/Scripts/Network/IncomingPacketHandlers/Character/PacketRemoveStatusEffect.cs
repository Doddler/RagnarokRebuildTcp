using Assets.Scripts.Effects;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.RemoveStatusEffect)]
    public class PacketRemoveStatusEffect : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var status = (CharacterStatusEffect)msg.ReadByte();

            if (Network.EntityList.TryGetValue(id, out var controllable))
            {
                Debug.Log($"Removing status {status} from {controllable.Name}");
                if (status == CharacterStatusEffect.Stun)
                {
                    controllable.EndEffectOfType(EffectType.Stun);
                    // controllable.SpriteAnimator.Color = new Color(1, 1, 1);
                    // if(controllable.SpriteAnimator.CurrentMotion == SpriteMotion.Idle)
                    //     controllable.SpriteAnimator.AnimSpeed = 1;
                }
                if (status == CharacterStatusEffect.TwoHandQuicken)
                {
                    controllable.SpriteAnimator.Color = new Color(1, 1, 1f);
                    RoSpriteTrailManager.Instance.RemoveTrailFromEntity(controllable);
                }
            }
        }
    }
}