using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.DivineProtection, SkillClass.None, SkillTarget.Passive)]
    public class DivineProtectionHandler : SkillHandlerBase
    {
        public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
        {
            owner.AddStat(CharacterStat.AddResistRaceDemon, lvl);
            owner.AddStat(CharacterStat.AddResistRaceUndead, lvl);
        }

        public override void RemovePassiveEffects(CombatEntity owner, int lvl)
        {
            owner.SubStat(CharacterStat.AddResistRaceDemon, lvl);
            owner.SubStat(CharacterStat.AddResistRaceUndead, lvl);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            throw new NotImplementedException();
        }
    }
}
