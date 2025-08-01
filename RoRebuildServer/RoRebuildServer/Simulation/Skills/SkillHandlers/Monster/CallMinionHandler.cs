using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster
{
    [MonsterSkillHandler(CharacterSkill.CallMinion, SkillClass.Magic, SkillTarget.Self)]
    public class CallMinionHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            if (source.Character.Type != CharacterType.Monster)
                return;
            source.Character.Monster.ResetSummonMonsterDeathTime();
        }
    }
}
