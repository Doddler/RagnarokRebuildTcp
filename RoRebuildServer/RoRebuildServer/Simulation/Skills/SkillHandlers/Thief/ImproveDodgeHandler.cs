using Microsoft.CodeAnalysis.CSharp.Syntax;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief
{
    [SkillHandler(CharacterSkill.ImproveDodge, SkillClass.None, SkillTarget.Passive)]
    public class ImproveDodgeHandler : SkillHandlerBase
    {
        public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
        {
            owner.AddStat(CharacterStat.AddFlee, lvl * 3);
        }

        public override void RemovePassiveEffects(CombatEntity owner, int lvl)
        {
            owner.SubStat(CharacterStat.AddFlee, lvl * 3);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            throw new Exception($"Passive skill ImproveDodge should not be called as a usable skill.");
        }
    }
}
