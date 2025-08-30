using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.LexDivina, SkillClass.Magic, SkillTarget.Enemy)]
public class LexDivinaHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null)
            return;

        if (!isIndirect)
        {
            source.ApplyAfterCastDelay(3f);
            source.ApplyCooldownForSupportSkillAction();
        }

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.LexDivina, lvl, DamageInfo.EmptyResult(source.Entity, target.Entity));

        source.TrySilenceTarget(target, 1000, 0.6f, 25f + 5f * lvl);
    }
}