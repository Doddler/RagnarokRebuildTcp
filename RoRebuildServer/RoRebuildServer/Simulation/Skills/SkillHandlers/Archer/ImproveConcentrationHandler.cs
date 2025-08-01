using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Archer
{
    [SkillHandler(CharacterSkill.ImproveConcentration, SkillClass.None, SkillTarget.Self)]
    public class ImproveConcentrationHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            var ch = source.Character;

            source.ApplyCooldownForSupportSkillAction();

            var dex = (int)float.Ceiling(source.GetStat(CharacterStat.Dex) * (0.02f + 0.01f * lvl));
            var agi = (int)float.Ceiling(source.GetStat(CharacterStat.Agi) * (0.02f + 0.01f * lvl));
            
            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.ImproveConcentration, 40f + 20 * lvl, agi, dex);
            source.AddStatusEffect(status);

            var map = source.Character.Map;
            Debug.Assert(map != null);

            using var targetList = EntityListPool.Get();
            map.GatherEnemiesInArea(source.Character, source.Character.Position, 4, targetList, false, false);
            foreach (var e in targetList)
            {
                if (e.TryGet<CombatEntity>(out var nearbyEnemy))
                    nearbyEnemy.RemoveStatusOfGroupIfExists("Hiding");
            }

            CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.ImproveConcentration, lvl, isIndirect);
        }
    }
}
