using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Stun, SkillClass.Physical)]
public class StunAttackHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 7;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        var res = source.CalculateCombatResult(target, 1f, 1, AttackFlags.Physical, CharacterSkill.Stun, AttackElement.None);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        var ch = source.Character;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Stun, lvl, res);

        if (!res.IsDamageResult)
            return;

        source.TryStunTarget(target, lvl * 200, res.AttackMotionTime);
    }
}