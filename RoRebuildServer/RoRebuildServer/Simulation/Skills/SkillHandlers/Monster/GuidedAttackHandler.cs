using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.GuidedAttack, SkillClass.Physical, SkillTarget.Enemy)]
public class GuidedAttackHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 7;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
            return;

        var res = source.CalculateCombatResult(target, 1f + lvl * 0.1f, 1, AttackFlags.Physical, CharacterSkill.GuidedAttack, AttackElement.None);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        var ch = source.Character;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.GuidedAttack, lvl, res);

        if (!res.IsDamageResult)
            return;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.GuidedAttack, 15f, 10 * lvl);
        source.AddStatusEffect(status);
    }
}