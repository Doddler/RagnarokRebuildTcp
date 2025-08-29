using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.LexAeterna, SkillClass.Magic, SkillTarget.Enemy)]
public class LexAeternaHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        if (target.HasBodyState(BodyStateFlags.Frozen) || target.HasBodyState(BodyStateFlags.Petrified))
            return SkillValidationResult.TargetStateIgnoresEffect;

        return base.ValidateTarget(source, target, position, lvl, isIndirect, isItemSource);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null)
            return;

        if(!isIndirect)
            source.ApplyAfterCastDelay(3f);

        var targetStatus = StatusEffectState.NewStatusEffect(CharacterStatusEffect.LexAeterna, 120, source.Character.Id);
        target.AddStatusEffect(targetStatus);

        var di = DamageInfo.EmptyResult(source.Entity, target.Entity);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Stop, lvl, di);
    }
}