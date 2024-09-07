using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.SkillHandlers;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.Attack)]
    public class PacketAttack : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            
            var id1 = msg.ReadInt32();
            var id2 = msg.ReadInt32();

            var hasSrc = Network.EntityList.TryGetValue(id1, out var controllable);
            var hasTarget = Network.EntityList.TryGetValue(id2, out var controllable2);

            var dir = (Direction)msg.ReadByte();
            var skill = (CharacterSkill)msg.ReadByte();
            var hits = msg.ReadByte();
            var resultType = (AttackResult)msg.ReadByte();
            var pos = msg.ReadPosition();
            var dmg = msg.ReadInt32();
            var motionTime = msg.ReadFloat();
            var showAttackAction = msg.ReadBoolean();

            var weaponClass = 0;

            var result = new AttackResultData()
            {
                Src = controllable,
                Target = controllable2,
                Skill = skill,
                Damage = dmg,
                HitCount = hits,
                MotionTime = motionTime,
                DamageTiming = motionTime,
                Result = resultType,
            };

            if (hasSrc)
            {
                if (hasTarget)
                {
                    var cd = controllable.transform.localPosition - controllable2.transform.localPosition;
                    cd.y = 0;
                    controllable2.CounterHitDir = cd.normalized;
                    //Debug.Log("Counter hit: " + cd);

                    if (controllable.WeaponClass == 12) //don't hardcode id for bow!! Change this!
                    {
                        var arrow = ArcherArrow.CreateArrow(controllable, controllable2.gameObject, motionTime);
                        //controllable2.Messages.SendHitEffect(controllable, motionTime + arrow.Duration);
                    }
                    //else
                    
                }
                else
                {
                    var v = dir.GetVectorValue();
                    controllable.CounterHitDir = new Vector3(v.x, 0, v.y);
                }

                if (controllable.SpriteAnimator.State != SpriteState.Dead)
                {
                    controllable.StopImmediate(pos, false);

                    controllable.SpriteAnimator.State = SpriteState.Standby;
                    controllable.LookAtOrDefault(controllable2, dir);

                    Network.AttackMotion(controllable, pos, dir, motionTime, controllable2);

                    //controllable.SetAttackAnimationSpeed(motionTime);
                    controllable.PerformBasicAttackMotion();
                }

                weaponClass = controllable.WeaponClass;
                
                if(resultType == AttackResult.Miss)
                    controllable.Messages.SendMissEffect(motionTime);
            }
            

            if (hasTarget)
            {
                if (result.Skill != CharacterSkill.None)
                    ClientSkillHandler.OnHitEffect(controllable2, ref result);
                else
                {
                    var hitType = 1;
                    if (resultType == AttackResult.CriticalDamage)
                        hitType = 2;
                    if (dmg > 0)
                        controllable2.Messages.SendHitEffect(controllable, motionTime, hitType);
                }
                
                if(dmg > 0)
                    controllable2.Messages.SendDamageEvent(controllable, motionTime, dmg, hits, resultType == AttackResult.CriticalDamage);
                
                //StartCoroutine(DamageEvent(dmg, damageTiming, hits, weaponClass, controllable2));
            }
        }
    }
}