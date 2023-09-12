using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers
{
    [SkillHandler(CharacterSkill.FireBolt)]
    public class FireBoltHandler : SkillHandlerBase
    {
        public override float GetCastTime(CombatEntity source, CombatEntity target, Position position, int lvl)
        {
            if (lvl < 0 || lvl > 10)
                lvl = 10;

            return 2f + lvl * 0.1f;
        }

        public override void Process(CombatEntity source, CombatEntity target, Position position, int lvl)
        {
            if (lvl < 0 || lvl > 10)
                lvl = 10;

            var res = source.CalculateCombatResult(target, 1, lvl, AttackFlags.Magical, AttackElement.Fire);
            source.PerformAttackAction(target);
            source.ExecuteCombatResult(res);

            var ch = source.Character;

            ch.Map?.GatherPlayersForMultiCast(ch);
            CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.FireBolt, lvl, res);
            CommandBuilder.ClearRecipients();
        }
    }
}
