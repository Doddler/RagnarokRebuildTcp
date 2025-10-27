using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.Suffragium, SkillClass.Magic, SkillTarget.Ally)]
public class SuffragiumHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        //if (source == target)
        //    return SkillValidationResult.CannotTargetSelf;

        return base.ValidateTarget(source, target, position, lvl, isIndirect, isItemSource);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        //if (target == null || source == target)
        //    return;

        if (!isIndirect)
            source.ApplyAfterCastDelay(2f);

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Suffragium, 40 - lvl * 10, lvl);
        target.AddStatusEffect(status);

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.Suffragium);
        GenericCastAndInformSupportSkill(source, target, CharacterSkill.Suffragium, lvl, ref res, isIndirect, true);
    }
}