using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Blacksmith;

[SkillHandler(CharacterSkill.WeaponryResearch, SkillClass.Physical, SkillTarget.Passive)]
public class WeaponryResearchHandler : SkillHandlerBase
{
    public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
    {
        owner.AddStat(CharacterStat.AddRefineAttackPower, lvl * 3);
        owner.AddStat(CharacterStat.AddHit, lvl * 2);
        owner.AddStat(CharacterStat.AddAccuracyRate, lvl * 2);
    }

    public override void RemovePassiveEffects(CombatEntity owner, int lvl)
    {
        owner.SubStat(CharacterStat.AddRefineAttackPower, lvl * 3);
        owner.SubStat(CharacterStat.AddHit, lvl * 2);
        owner.SubStat(CharacterStat.AddAccuracyRate, lvl * 2);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        throw new NotImplementedException();
    }
}
