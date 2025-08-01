using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.RangedAttack, SkillClass.Physical)]
public class RangedAttackHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 7;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        var res = source.CalculateCombatResult(target, 1f + 0.2f * lvl, 1, AttackFlags.Physical, CharacterSkill.RangedAttack, AttackElement.None);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.RangedAttack, lvl, res);
    }
}