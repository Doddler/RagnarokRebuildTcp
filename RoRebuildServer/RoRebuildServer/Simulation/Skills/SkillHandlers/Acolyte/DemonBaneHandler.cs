using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.DemonBane, SkillClass.None, SkillTarget.Passive)]
    public class DemonBaneHandler : SkillHandlerBase
    {
        public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
        {
            owner.AddStat(CharacterStat.PercentVsDemon, lvl);
            owner.AddStat(CharacterStat.PercentVsUndead, lvl);
        }

        public override void RemovePassiveEffects(CombatEntity owner, int lvl)
        {
            owner.SubStat(CharacterStat.PercentVsDemon, lvl);
            owner.SubStat(CharacterStat.PercentVsUndead, lvl);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            throw new NotImplementedException();
        }
    }
}
