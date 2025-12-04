using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Archer
{
    [SkillHandler(CharacterSkill.VultureEye, SkillClass.None, SkillTarget.Passive)]
    public class VultureEyeHandler : SkillHandlerBase
    {
        //bonus range will be applied based on equipped weapon as part of the update character stats function

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            throw new NotImplementedException();
        }

        public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
        {
            owner.AddStat(CharacterStat.AddHit, lvl);
        }

        public override void RemovePassiveEffects(CombatEntity owner, int lvl)
        {
            owner.SubStat(CharacterStat.AddHit, lvl);
        }
    }
}