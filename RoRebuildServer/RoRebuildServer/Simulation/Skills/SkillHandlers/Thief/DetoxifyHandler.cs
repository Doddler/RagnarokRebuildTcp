using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief;

[SkillHandler(CharacterSkill.Detoxify, SkillClass.Physical, SkillTarget.Ally)]
public class DetoxifyHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
            return;

        if (!target.RemoveStatusOfTypeIfExists(CharacterStatusEffect.Poison))
        {
            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Detoxify, 60f, lvl);
            target.AddStatusEffect(status);
        }

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.Detoxify);
        GenericCastAndInformSupportSkill(source, target, CharacterSkill.Detoxify, lvl, ref res, isIndirect, true);
    }
}