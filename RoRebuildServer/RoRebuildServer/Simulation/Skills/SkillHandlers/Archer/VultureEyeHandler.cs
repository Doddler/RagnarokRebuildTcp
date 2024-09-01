using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Archer
{
    [SkillHandler(CharacterSkill.VultureEye, SkillClass.None, SkillTarget.Passive)]
    public class VultureEyeHandler : SkillHandlerBase
    {
        //no need for passive handler, stat handler will apply the bonus range (but we need this handler to mark it as passive)

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            throw new NotImplementedException();
        }
    }
}
