using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage
{
    [SkillHandler(CharacterSkill.FrostDiver, SkillClass.Magic)]
    public class FrostDiverHandler : SkillHandlerBase

    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            
        }
    }
}
