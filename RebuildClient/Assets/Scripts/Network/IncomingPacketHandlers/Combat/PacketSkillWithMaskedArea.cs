using System;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.SkillHandlers;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.SkillWithMaskedArea)]
    public class PacketSkillWithMaskedArea : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var target = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var skill = (CharacterSkill)msg.ReadByte();
            var skillLvl = (int)msg.ReadByte();
            var dir = (Direction)msg.ReadByte();
            var pos = msg.ReadPosition();
            var range = (int)msg.ReadByte();
            var motionTime = msg.ReadFloat();
            var isIndirect = msg.ReadBoolean();

            var size = (1 + range * 2) * (1 + range * 2);

            Span<bool> mask = stackalloc bool[size];

            for (var i = 0; i < size; i++)
                mask[i] = msg.ReadBoolean();
            
            
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

            if (!isIndirect)
            {
                if (target == controllable.CellPosition)
                    controllable.LookInDirection(dir);
                else
                    controllable.LookAt(target.ToWorldPosition());
                // controllable.SnapToTile(pos, 0.03f, 1f);
                Network.PrepareAttackMotionSettings(controllable, pos, dir, motionTime, null);
                ClientSkillHandler.ExecuteSkill(controllable, ref attack);
            }

            switch (skill)
            {
                case CharacterSkill.HeavensDrive:
                    HeavensDriveEffect.Create(target, mask, motionTime - 0.183f);
                    break;
            }
        }
    }
}