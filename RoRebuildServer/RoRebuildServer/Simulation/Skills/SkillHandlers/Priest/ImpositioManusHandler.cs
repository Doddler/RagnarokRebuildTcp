using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.ImpositioManus, SkillClass.Magic, SkillTarget.Ally)]
public class ImpositioManusHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null)
            return;

        if (!isIndirect)
            source.ApplyAfterCastDelay(3f);

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.ImpositioManus, 60, lvl);
        target.AddStatusEffect(status);

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.ImpositioManus);
        GenericCastAndInformSupportSkill(source, target, CharacterSkill.ImpositioManus, lvl, ref res, isIndirect, true);
    }
}