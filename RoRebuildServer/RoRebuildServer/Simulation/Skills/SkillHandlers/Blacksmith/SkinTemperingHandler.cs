using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Blacksmith;

[SkillHandler(CharacterSkill.SkinTempering, SkillClass.Physical, SkillTarget.Passive)]
public class SkinTemperingHandler : SkillHandlerBase
{
    public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
    {
        owner.AddStat(CharacterStat.AddResistElementNeutral, lvl);
        owner.AddStat(CharacterStat.AddResistElementFire, lvl * 4);
    }

    public override void RemovePassiveEffects(CombatEntity owner, int lvl)
    {
        owner.SubStat(CharacterStat.AddResistElementNeutral, lvl);
        owner.SubStat(CharacterStat.AddResistElementFire, lvl * 4);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        throw new NotImplementedException();
    }
}

