using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Petrify, SkillClass.Magic)]
public class PetrifyAttackHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        var res = source.CalculateCombatResult(target, 1f, 1, AttackFlags.Physical, CharacterSkill.Petrify, AttackElement.None);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Petrify, lvl, res);

        if (!res.IsDamageResult)
            return;

        source.TryPetrifyTarget(target, lvl * 200, 0.3f, res.AttackMotionTime);
    }
}