using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman
{
    [SkillHandler(CharacterSkill.Bash, SkillClass.Physical)]
    public class BashHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            lvl = lvl.Clamp(1, 10);
            
            if (target == null || !target.IsValidTarget(source))
                return;

            var req = new AttackRequest(CharacterSkill.Bash, 1f + lvl * 0.3f, 1, AttackFlags.Physical, AttackElement.None);
            req.AccuracyRatio = 100 + lvl * 5;
            var res = source.CalculateCombatResult(target, req);

            source.ApplyCooldownForAttackAction(target);
            source.ExecuteCombatResult(res, false);
            
            CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Bash, lvl, res);
        }
    }
}
