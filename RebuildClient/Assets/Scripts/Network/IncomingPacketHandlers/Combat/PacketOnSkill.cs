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
                AttackerPos = pos,
                MotionTime = motionTime,
                DamageTiming = motionTime
            };

            if(target == controllable.CellPosition)
                controllable.LookInDirection(dir);
            else
                controllable.LookAt(target.ToWorldPosition());
            // controllable.SnapToTile(pos, 0.03f, 1f);
            Network.PrepareAttackMotionSettings(controllable, pos, dir, motionTime, null);
            ClientSkillHandler.ExecuteSkill(controllable, ref attack);
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
            
            controllable.ShowSkillCastMessage(skill, 3);

            Network.PrepareAttackMotionSettings(controllable, pos, dir, motionTime, null);
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
            var damageTiming = msg.ReadFloat();

            var attack = new AttackResultData()
            {
                Skill = skill,
                SkillLevel = (byte)skillLvl,
                Result = result,
                Damage = dmg,
                HitCount = hits,
                MotionTime = motionTime,
                DamageTiming = damageTiming,
                Target = controllable2,
                Src = controllable
            };

            if (!hasSource)
            {
                //if the skill handler is not flagged to execute without a source this will do nothing.
                //we still want to execute when a special effect plays on a target though.
                ClientSkillHandler.ExecuteSkill(null, ref attack);

                if (hits > 0 && result != AttackResult.Miss && result != AttackResult.Invisible && controllable2 != null)
                {
                    controllable2?.Messages.SendDamageEvent(null, motionTime, dmg, hits, result == AttackResult.CriticalDamage);
                    if(dmg > 0)
                        ClientSkillHandler.OnHitEffect(controllable2, ref attack);
                }

                return;
            }
            
            if(controllable != controllable2)
                controllable.LookAtOrDefault(controllable2, dir);

            if (result == AttackResult.Heal || result == AttackResult.Invisible)
                motionTime = 0;
            else
                Network.PrepareAttackMotionSettings(controllable, pos, dir, motionTime, controllable2);

            controllable.ShowSkillCastMessage(skill, 3);
            // controllable.SnapToTile(pos, 0.03f, 1f);

            if (result == AttackResult.Miss)
            {
                for(var i = 0; i < attack.HitCount; i++)
                    controllable.Messages.SendMissEffect(motionTime + 0.2f * i);
            }

            if (hasTarget)
            {
                ClientSkillHandler.ExecuteSkill(controllable, ref attack);

                if (result == AttackResult.Heal && dmg != 0)
                    hits = 1;

                if (hits > 0 && result != AttackResult.Miss && result != AttackResult.Invisible)
                {
                    controllable2.Messages.SendDamageEvent(controllable, damageTiming, dmg, hits, result == AttackResult.CriticalDamage);
                    if(dmg > 0)
                        ClientSkillHandler.OnHitEffect(controllable2, ref attack);
                }
            }
        }

    }
}