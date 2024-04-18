using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman
{
    [SkillHandler(CharacterSkill.Bash, SkillClass.Physical)]
    public class BashHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl)
        {
            if (lvl < 0 || lvl > 10)
                lvl = 10;

            if (target == null || !target.IsValidTarget(source))
                return;

            var res = source.CalculateCombatResult(target, 2f + lvl * 0.2f, 1, AttackFlags.Physical);
            source.ApplyCooldownForAttackAction(target);
            source.ExecuteCombatResult(res, false);
            
            var ch = source.Character;

            ch.Map?.GatherPlayersForMultiCast(ch);
            CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.Bash, lvl, res);
            CommandBuilder.ClearRecipients();
        }
    }
}
