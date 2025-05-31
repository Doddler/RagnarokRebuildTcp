using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[SkillHandler(CharacterSkill.EarthSpike, SkillClass.Magic)]
public class EarthSpikeHandler : TargetedSpellBase
{
    public override CharacterSkill GetSkill() => CharacterSkill.EarthSpike;
    public override AttackElement GetElement() => AttackElement.Earth;
}