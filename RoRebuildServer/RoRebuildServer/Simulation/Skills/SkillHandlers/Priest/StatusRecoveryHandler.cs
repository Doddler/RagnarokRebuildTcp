using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.StatusRecovery, SkillClass.Magic, SkillTarget.Ally)]
public class StatusRecoveryHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null)
        {
            ServerLogger.LogWarning($"Entity {source.Character.Name} is attempting to cast Cure without a target.");
            return;
        }

        target.CleanseStatusEffect(StatusCleanseTarget.Petrify | StatusCleanseTarget.Frozen | StatusCleanseTarget.Stunned | StatusCleanseTarget.Sleep);

        if (!isIndirect)
            source.ApplyAfterCastDelay(2f);

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.StatusRecovery);
        GenericCastAndInformSupportSkill(source, target, CharacterSkill.StatusRecovery, lvl, ref res, isIndirect, true);
    }
}