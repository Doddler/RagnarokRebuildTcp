using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage
{
    [SkillHandler(CharacterSkill.ColdBolt, SkillClass.Magic)]
    public class ColdBoltHandler : TargetedSpellBase
    {
        public override CharacterSkill GetSkill() => CharacterSkill.ColdBolt;
        public override AttackElement GetElement() => AttackElement.Water;
    }
}