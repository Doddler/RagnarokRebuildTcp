using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Hallucination, SkillClass.Magic)]
public class HallucinationHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 9;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
            return;

        source.ApplyCooldownForAttackAction(target);

        if (!source.CheckLuckModifiedRandomChanceVsTarget(target, 200 * lvl, 1000))
            return;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Hallucination, 30);
        target.AddStatusEffect(status, true, 0);
    }
}