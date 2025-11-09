using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Network.Messaging;
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
            var offHand = msg.ReadInt32();
            var motionTime = msg.ReadFloat();
            var damageTime = msg.ReadFloat();
            var showAttackAction = msg.ReadBoolean();

            var comboDelay = 0.2f;

            var hasOffHand = offHand != 0;

            //this is a hack, but we want to make sure the client attack motion is slightly longer than the server so attacks can be seamlessly chained.
            if (hasSrc)
                motionTime = Mathf.Min(motionTime * 1.1f, motionTime + 0.1f);

            var result = new AttackResultData()
            {
                Src = controllable,
                Target = controllable2,
                Skill = skill,
                Damage = dmg,
                HitCount = hits,
                MotionTime = motionTime,
                DamageTiming = damageTime,
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

                    if (controllable.WeaponClass == 12 && skill == CharacterSkill.None) //don't hardcode id for bow!! Change this!
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

                if (controllable.SpriteAnimator.State != SpriteState.Dead && showAttackAction)
                {
                    controllable.StopImmediate(pos, false);

                    controllable.SpriteAnimator.State = SpriteState.Standby;
                    controllable.LookAtOrDefault(controllable2, dir);

                    Network.PrepareAttackMotionSettings(controllable, pos, dir, motionTime, controllable2);

                    //controllable.SetAttackAnimationSpeed(motionTime);
                    controllable.PerformBasicAttackMotion();
                }
                
                if (resultType == AttackResult.Miss)
                    controllable.Messages.SendMissEffect(damageTime);

                if (hasOffHand && offHand < 0)
                    controllable.Messages.SendMissEffect(damageTime + 0.1f);
            }

            if (hasTarget && resultType != AttackResult.InvisibleMiss)
            {
                if (result.Skill != CharacterSkill.NoCast)
                {
                    if (result.Skill != CharacterSkill.None && result.Damage > 0)
                        ClientSkillHandler.OnHitEffect(controllable2, ref result);
                    else
                    {
                        var hitType = 1;
                        if (resultType == AttackResult.CriticalDamage)
                            hitType = 2;
                        if (dmg > 0)
                            controllable2.Messages.SendHitEffect(controllable, damageTime, hitType);
                        if (offHand > 0)
                            controllable2.Messages.SendHitEffect(controllable, damageTime + 0.3f, hitType);
                    }
                }

                if (result.Result == AttackResult.LuckyDodge)
                {
                    controllable2.Messages.SendMessage(EntityMessageType.LuckyDodge, motionTime);
                    return;
                }
                
                if (result.Result == AttackResult.Block)
                {
                    controllable2.Messages.SendBlockEvent(motionTime);
                    return;
                }

                if (dmg > 0)
                {
                    var dmgSound = ClientSkillHandler.SkillTakesWeaponSound(skill);
                    // Debug.Log($"Offhand:{hasOffHand} {dmg}x{hits} + {offHand}");
                    if (resultType != AttackResult.Invisible)
                    {
                        if (hasOffHand)
                            controllable2.Messages.SendDualWieldingDamageEvent(controllable, damageTime, dmg, offHand, hits,
                                resultType == AttackResult.CriticalDamage);
                        else
                            controllable2.Messages.SendDamageEvent(controllable, damageTime, dmg, hits, resultType == AttackResult.CriticalDamage, dmgSound,
                                result.Skill != CharacterSkill.Smoking);
                    }
                }

                //StartCoroutine(DamageEvent(dmg, damageTiming, hits, weaponClass, controllable2));
            }
        }
    }
}