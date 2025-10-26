using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.SkillHandlers;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    //a targeted skill attack where the source is either not known or we probably don't care. Usually aoe damage.
    [ClientPacketHandler(PacketType.SkillIndirect)]
    public class PacketSkillIndirect : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id1 = msg.ReadInt32();
            var id2 = msg.ReadInt32();
            var pos = msg.ReadPosition();
            var damage = msg.ReadInt32();
            var time = msg.ReadFloat();
            var skill = (CharacterSkill)msg.ReadByte();
            var hitCount = msg.ReadByte();
            var result = (AttackResult)msg.ReadByte();
            
            var hasSource = Network.EntityList.TryGetValue(id1, out var attacker);
            var hasTarget = Network.EntityList.TryGetValue(id2, out var target);
            
                        
            if (result == AttackResult.InvisibleMiss)
            {
                hasTarget = false;
                target = null;
            }

            var attack = new AttackResultData()
            {
                Skill = skill,
                SkillLevel = 11,
                Result = result,
                Damage = damage,
                HitCount = hitCount,
                MotionTime = 0,
                DamageTiming = time,
                Target = target
            };
            
            // if(hasTarget)
            //     target.LookAt(pos.ToWorldPosition());
            
            if (result == AttackResult.Miss && hasSource)
            {
                for(var i = 0; i < attack.HitCount; i++)
                    attacker.Messages.SendMissEffect(0.2f * i);
            }
            
            if (hasTarget && target.SpriteAnimator.IsInitialized)
            {
                ClientSkillHandler.OnHitEffect(target, ref attack);

                if (result == AttackResult.Block)
                {
                    target.Messages.SendBlockEvent(time);
                    return;
                }

                if(hitCount > 0 && result != AttackResult.Miss && result != AttackResult.Invisible)
                    target.Messages.SendDamageEvent(attacker, time, damage, hitCount, result == AttackResult.CriticalDamage);
            }

        }
    }
}