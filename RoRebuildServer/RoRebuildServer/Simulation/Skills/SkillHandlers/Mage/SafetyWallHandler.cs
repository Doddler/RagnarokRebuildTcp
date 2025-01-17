using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.SafetyWall, SkillClass.Magic, SkillTarget.Ground)]
public class SafetyWallHandler : SkillHandlerBase
{
        
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        throw new NotImplementedException();
    }
}