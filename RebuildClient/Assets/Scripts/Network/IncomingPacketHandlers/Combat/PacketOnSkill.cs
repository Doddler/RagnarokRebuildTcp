using System;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.SkillHandlers;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.Skill)]
    public class PacketOnSkill : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var type = (SkillTarget)msg.ReadByte();
            // Debug.Log($"Skill type {type}");
            switch (type)
            {
                case SkillTarget.Ground:
                    OnMessageAreaTargetedSkill(msg);
                    break;
                case SkillTarget.Self:
                    OnMessageSelfTargetedSkill(msg);
                    break;
                case SkillTarget.Enemy:
                case SkillTarget.Ally:
                case SkillTarget.Any:
                    OnMessageTargetedSkillAttack(msg);
                    break;
                default:
                    throw new Exception($"Could not handle skill packet of type {type}");
            }
        }
        
        
        private void OnMessageAreaTargetedSkill(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var target = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var skill = (CharacterSkill)msg.ReadByte();
            var skillLvl = (int)msg.ReadByte();
            var dir = (Direction)msg.ReadByte();
            var pos = msg.ReadPosition();
            var motionTime = msg.ReadFloat();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            var attack = new AttackResultData()
            {
                Skill = skill,
                SkillLevel = (byte)skillLvl,
                TargetAoE = target,
                MotionTime = motionTime,
                DamageTiming = motionTime
            };

            ClientSkillHandler.ExecuteSkill(controllable, ref attack);
            controllable.LookAt(target.ToWorldPosition());
            Network.AttackMotion(controllable, pos, dir, motionTime, null);
        }
        
        
        private void OnMessageSelfTargetedSkill(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var skill = (CharacterSkill)msg.ReadByte();
            var skillLvl = (int)msg.ReadByte();
            var dir = (Direction)msg.ReadByte();
            var pos = msg.ReadPosition();
            var motionTime = msg.ReadFloat();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;
            
            var attack = new AttackResultData()
            {
                Skill = skill,
                SkillLevel = (byte)skillLvl,
                Target = controllable,
                MotionTime = motionTime,
                DamageTiming = motionTime,
            };

            Network.AttackMotion(controllable, pos, dir, motionTime, null);
            ClientSkillHandler.ExecuteSkill(controllable, ref attack);
        }

        private void OnMessageTargetedSkillAttack(ClientInboundMessage msg)
        {
            var id1 = msg.ReadInt32();
            var id2 = msg.ReadInt32();
            var skill = (CharacterSkill)msg.ReadByte();
            var skillLvl = (int)msg.ReadByte();

            var hasSource = Network.EntityList.TryGetValue(id1, out var controllable);
            var hasTarget = Network.EntityList.TryGetValue(id2, out var controllable2);

            var dir = (Direction)msg.ReadByte();
            var pos = msg.ReadPosition();
            var dmg = msg.ReadInt32();
            var result = (AttackResult)msg.ReadByte();
            var hits = msg.ReadByte();
            var motionTime = msg.ReadFloat();
            // var moveAfter = msg.ReadBoolean();

            var attack = new AttackResultData()
            {
                Skill = skill,
                SkillLevel = (byte)skillLvl,
                Result = result,
                Damage = dmg,
                HitCount = hits,
                MotionTime = motionTime,
                DamageTiming = motionTime,
                Target = controllable2
            };

            if (!hasSource)
            {
                //if the skill handler is not flagged to execute without a source this will do nothing.
                //we still want to execute when a special effect plays on a target though.

                ClientSkillHandler.ExecuteSkill(null, ref attack);
                //StartCoroutine(DamageEvent(dmg, 0f, hits, 0, controllable2));
                
                if(dmg != 0)
                    controllable2?.Messages.SendDamageEvent(null, motionTime, dmg, hits, result == AttackResult.CriticalDamage);
                return;
            }
            
            if(controllable != controllable2)
                controllable.LookAtOrDefault(controllable2, dir);

            if (result == AttackResult.Heal || result == AttackResult.Invisible)
            {
                motionTime = 0;
            }
            else
            {
                // if (hasTarget)
                // {
                //     if (controllable.WeaponClass == 12) //don't hardcode id for bow!! Change this!
                //     {
                //         var arrow = ArcherArrow.CreateArrow(controllable.gameObject, controllable2.gameObject, motionTime);
                //         controllable2.Messages.SendHitEffect(controllable, motionTime + arrow.Duration);
                //     }
                //     else
                //         controllable2.Messages.SendHitEffect(controllable, motionTime);
                // }

                Network.AttackMotion(controllable, pos, dir, motionTime, controllable2);
            }

            controllable.ShowSkillCastMessage(skill, 3);

            if (result == AttackResult.Miss)
            {
                for(var i = 0; i < attack.HitCount; i++)
                    controllable.Messages.SendMissEffect(motionTime + 0.2f * i);
            }

            if (hasTarget && controllable.SpriteAnimator.IsInitialized)
            {
                if (controllable.SpriteAnimator.SpriteData == null)
                {
                    throw new Exception("AAA? " + controllable.gameObject.name + " " + controllable.gameObject);
                }

                ClientSkillHandler.ExecuteSkill(controllable, ref attack);

                if (result == AttackResult.Heal && dmg != 0)
                    hits = 1;
                
                if(hits > 0 && result != AttackResult.Miss && result != AttackResult.Invisible)
                    controllable2.Messages.SendDamageEvent(controllable, motionTime, dmg, hits, result == AttackResult.CriticalDamage);
                //StartCoroutine(DamageEvent(dmg, motionTime, hits, controllable.WeaponClass, controllable2));
            }
        }

    }
}