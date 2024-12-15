using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.DemonBane, SkillClass.None, SkillTarget.Passive)]
    public class DemonBaneHandler : SkillHandlerBase
    {
        public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
        {
            owner.AddStat(CharacterStat.AddAttackRaceDemon, lvl);
            owner.AddStat(CharacterStat.AddAttackRaceUndead, lvl);
        }

        public override void RemovePassiveEffects(CombatEntity owner, int lvl)
        {
            owner.SubStat(CharacterStat.AddAttackRaceDemon, lvl);
            owner.SubStat(CharacterStat.AddAttackRaceUndead, lvl);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            throw new NotImplementedException();
        }
    }
}
