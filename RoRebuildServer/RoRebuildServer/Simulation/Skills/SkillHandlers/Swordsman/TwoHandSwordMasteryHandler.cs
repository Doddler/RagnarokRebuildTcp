using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman;

[SkillHandler(CharacterSkill.TwoHandSwordMastery, SkillClass.None, SkillTarget.Passive)]
public class TwoHandSwordMasteryHandler : SkillHandlerBase
{
    //weapon specific mastery skills are handled under mastery bonus in CombatEntity

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        throw new NotImplementedException();
    }
}