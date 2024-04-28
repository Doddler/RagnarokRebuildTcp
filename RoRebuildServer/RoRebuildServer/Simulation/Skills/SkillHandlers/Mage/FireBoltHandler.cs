using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.FireBolt, SkillClass.Magic)]
public class FireBoltHandler : TargetedSpellBase
{
    public override CharacterSkill GetSkill() => CharacterSkill.FireBolt;
    public override AttackElement GetElement() => AttackElement.Fire;
}

