using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[SkillHandler(CharacterSkill.SpringTrap, SkillClass.Physical, SkillTarget.Any)]
public class SpringTrapHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        throw new NotImplementedException();
    }
}