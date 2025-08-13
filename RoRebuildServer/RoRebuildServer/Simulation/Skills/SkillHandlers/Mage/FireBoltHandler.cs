using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.FireBolt, SkillClass.Magic)]
public class FireBoltHandler : TargetedSpellBase
{
    public override CharacterSkill GetSkill() => CharacterSkill.FireBolt;
    public override AttackElement GetElement() => AttackElement.Fire;
}

