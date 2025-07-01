using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[SkillHandler(CharacterSkill.Cure, SkillClass.Magic, SkillTarget.Ally)]
public class CureHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
        {
            ServerLogger.LogWarning($"Entity {source.Character.Name} is attempting to cast Cure without a target.");
            return;
        }

        target.CleanseStatusEffect(StatusCleanseTarget.Blind | StatusCleanseTarget.Confusion | StatusCleanseTarget.Silence);

        if(!isIndirect)
            source.ApplyAfterCastDelay(0.5f);
        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.Cure);
        GenericCastAndInformSupportSkill(source, target, CharacterSkill.Cure, lvl, ref res, isIndirect, true);
    }
}