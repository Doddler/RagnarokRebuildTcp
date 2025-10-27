using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.MaceMastery, SkillClass.None, SkillTarget.Passive)]
public class MaceMasteryHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        //nothing!
    }
}