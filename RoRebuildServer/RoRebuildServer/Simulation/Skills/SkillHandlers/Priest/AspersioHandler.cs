using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.Aspersio, SkillClass.Magic, SkillTarget.Ally)]
public class AspersioHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (!isIndirect && !isItemSource && !CheckRequiredItem(source, HolyWater, false))
            return SkillValidationResult.MissingRequiredItem;

        return base.StandardValidationForAllyTargetedAttack(source, target, position);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        if (!isIndirect && !ConsumeItemForSkillWithFailMessage(source, HolyWater))
            return;

        if (!isIndirect)
        {
            source.ApplyCooldownForSupportSkillAction();
            source.ApplyAfterCastDelay(2f);
        }

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Aspersio, 30 + 30 * lvl, 5);
        target.AddStatusEffect(status);

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.Aspersio);
        GenericCastAndInformSupportSkill(source, target, CharacterSkill.Aspersio, lvl, ref res, isIndirect, true);
    }
}