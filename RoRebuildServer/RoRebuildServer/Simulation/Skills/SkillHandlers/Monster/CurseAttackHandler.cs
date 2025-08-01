using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Curse, SkillClass.Magic)]
public class CurseAttackHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 7;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        var res = source.CalculateCombatResult(target, 1.2f, 1, AttackFlags.Physical, CharacterSkill.Curse, AttackElement.Dark);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Curse, lvl, res);

        if (!res.IsDamageResult)
            return;

        source.TryCurseTarget(target, lvl * 200, res.AttackMotionTime);
    }
}