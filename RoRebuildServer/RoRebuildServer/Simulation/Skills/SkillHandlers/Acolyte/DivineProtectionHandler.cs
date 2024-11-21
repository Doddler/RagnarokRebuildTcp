using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.DivineProtection, SkillClass.None, SkillTarget.Passive)]
    public class DivineProtectionHandler : SkillHandlerBase
    {
        public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
        {
            owner.AddStat(CharacterStat.ReductionFromDemon, lvl);
            owner.AddStat(CharacterStat.ReductionFromUndead, lvl);
        }

        public override void RemovePassiveEffects(CombatEntity owner, int lvl)
        {
            owner.SubStat(CharacterStat.ReductionFromDemon, lvl);
            owner.SubStat(CharacterStat.ReductionFromUndead, lvl);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            throw new NotImplementedException();
        }
    }
}
