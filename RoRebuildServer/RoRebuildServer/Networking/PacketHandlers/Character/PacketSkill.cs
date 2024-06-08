using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Skills;

namespace RoRebuildServer.Networking.PacketHandlers.Character
{
    [ClientPacketHandler(PacketType.Skill)]
    public class PacketSkill : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (!connection.IsPlayerAlive)
                return;

            var type = (SkillTarget)msg.ReadByte();

            switch (type)
            {
                case SkillTarget.Ground:
                    ProcessGroundTargetedSkill(connection, msg);
                    return;
                case SkillTarget.Enemy: 
                    ProcessSingleTargetSkill(connection, msg);
                    return;
            }

            ServerLogger.Log($"Player {connection.Character.Name} is attempting to use a skill of type {type}, but no handler exists for this class of skill");
            return;
        }

        private void ProcessGroundTargetedSkill(NetworkConnection connection, InboundMessage msg)
        {
            Debug.Assert(connection.Player != null, "connection.Player != null");

            var caster = connection.Character;
            var groundTarget= msg.ReadPosition();
            var map = caster?.Map;

            if (map == null || caster == null)
                throw new Exception($"Cannot ProcessGroundTargetedSkill, player or map is invalid.");

            if (!map.WalkData.HasLineOfSight(caster.Position, groundTarget))
            {
                CommandBuilder.SkillFailed(connection.Player, SkillValidationResult.NoLineOfSight);
                return;
            }

            var skill = (CharacterSkill)msg.ReadByte();
            var lvl = (int)msg.ReadByte();

            caster.ResetSpawnImmunity();
            caster.CombatEntity.AttemptStartGroundTargetedSkill(groundTarget, skill, lvl);
        }

        private void ProcessSingleTargetSkill(NetworkConnection connection, InboundMessage msg)
        {
            Debug.Assert(connection.Player != null, "connection.Player != null");

            var caster = connection.Character;
            var targetEntity = World.Instance.GetEntityById(msg.ReadInt32());
            var target = targetEntity.GetIfAlive<CombatEntity>();
            if (target == null || caster == null)
                return;
            
            var skill = (CharacterSkill)msg.ReadByte();
            var lvl = (int)msg.ReadByte();

            caster.ResetSpawnImmunity();
            caster.CombatEntity.AttemptStartSingleTargetSkillAttack(target, skill, lvl);
        }
    }
}
