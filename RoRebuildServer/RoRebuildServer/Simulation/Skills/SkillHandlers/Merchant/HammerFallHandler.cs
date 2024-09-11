using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Merchant
{
    [SkillHandler(CharacterSkill.HammerFall, SkillClass.Physical, SkillTarget.Ground)]
    public class HammerFallHandler : SkillHandlerBase
    {
        public override bool IsAreaTargeted => true;

        //casting range is 2, unless a monster uses it, they can use it up to 7 tiles away
        public override int GetSkillRange(CombatEntity source, int lvl) => source.Character.Type == CharacterType.Monster ? 7 : 2;

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            if (!position.IsValid())
                return;

            using var targetList = EntityListPool.Get();
            var map = source.Character.Map!;
            var aoeSize = lvl <= 5 ? 2 : 9;
            var rate = 20 + lvl * 10;
            
            map.GatherEnemiesInArea(source.Character, position, aoeSize, targetList, !isIndirect, true);

            //deal damage to all enemies
            foreach (var e in targetList)
            {
                if (!e.TryGet<CombatEntity>(out var enemy))
                    continue;

                if (enemy.HasStatusEffectOfType(CharacterStatusEffect.Stun))
                    continue;

                var vit = enemy.GetEffectiveStat(CharacterStat.Vit);
                
                var resist = MathF.Pow(0.99f, vit);
                if (GameRandom.NextFloat(0, 100) > rate * resist)
                    continue;

                var len = 5f * resist;

                var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Stun, len);
                enemy.AddStatusEffect(status, false, 0.25f);
            }

            source.ApplyCooldownForAttackAction();
           
            map.SendVisualEffectToPlayers(DataManager.EffectIdForName["HammerFall"], position, 0);

            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(source.Character, position, CharacterSkill.HammerFall, lvl);
        }
    }
}
