using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.LightningBolt, SkillClass.Magic)]
public class LightningBoltHandler : TargetedSpellBase
{
    public override CharacterSkill GetSkill() => CharacterSkill.LightningBolt;
    public override AttackElement GetElement() => AttackElement.Wind;
}