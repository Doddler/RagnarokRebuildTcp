using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Stop, SkillClass.Physical)]
public class StopHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 2;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.HasBodyState(BodyStateFlags.Stopped) || target == null || !target.IsValidTarget(source) ||
            target.HasBodyState(BodyStateFlags.Stopped))
            return SkillValidationResult.Failure;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        source.ApplyCooldownForSupportSkillAction();
        source.Character.StopMovingImmediately();
        target.Character.StopMovingImmediately();

        var srcStatus = StatusEffectState.NewStatusEffect(CharacterStatusEffect.StopOwner, 15f, target.Character.Id);
        source.AddStatusEffect(srcStatus);

        var targetStatus = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Stop, 15f, source.Character.Id);
        target.AddStatusEffect(targetStatus);

        var di = DamageInfo.EmptyResult(source.Entity, target.Entity);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Stop, lvl, di);
    }
}