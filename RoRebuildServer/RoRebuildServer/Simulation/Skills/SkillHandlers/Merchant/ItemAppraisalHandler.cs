using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant;

[SkillHandler(CharacterSkill.ItemAppraisal, SkillClass.Physical, SkillTarget.Passive)]
public class ItemAppraisalHandler : SkillHandlerBase
{
    public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
    {
        owner.AddStat(CharacterStat.AddHpItemEffectivenessPercent, lvl * 2);
        owner.AddStat(CharacterStat.AddSpItemEffectivenessPercent, lvl * 2);
    }

    public override void RemovePassiveEffects(CombatEntity owner, int lvl)
    {
        owner.SubStat(CharacterStat.AddHpItemEffectivenessPercent, lvl * 2);
        owner.SubStat(CharacterStat.AddSpItemEffectivenessPercent, lvl * 2);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        throw new NotImplementedException();
    }
}
