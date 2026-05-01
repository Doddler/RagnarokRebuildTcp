using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster
{
    [SkillHandler(CharacterSkill.NoCast, SkillClass.None, SkillTarget.Self)]
    public class NoCastHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            //do nothing!
        }
    }
}