using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Blacksmith
{
    [SkillHandler(CharacterSkill.HammerFall, SkillClass.Physical, SkillTarget.Ground)]
    public class HammerFallHandler : SkillHandlerBase
    {
        public override bool IsAreaTargeted => true;

        //casting range is 2, unless a monster uses it, they can use it up to 7 tiles away
        public override int GetSkillRange(CombatEntity source, int lvl) => source.Character.Type == CharacterType.Monster ? 7 : 2;

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
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

                if (enemy.HasStatusEffectOfType(CharacterStatusEffect.Stun) || enemy.GetSpecialType() == CharacterSpecialType.Boss)
                    continue;

                if (enemy.IsCasting && enemy.CastInterruptionMode == CastInterruptionMode.NeverInterrupt)
                    continue;

                source.TryStunTarget(enemy, rate * 10);

                //var vit = enemy.GetEffectiveStat(CharacterStat.Vit) * 3 / 2; //+50% to make it less weird
                ////if (vit > 75 && enemy.Character.Type == CharacterType.Player)
                ////    vit += (vit - 75);

                //var resist = 1f - enemy.GetStat(CharacterStat.ResistStun) / 100f;
                //var statResist = MathF.Pow(0.99f, vit);

                //if (GameRandom.NextFloat(0, 100) > rate * resist * statResist)
                //    continue;

                //var len = 5f * statResist;

                //var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Stun, len);
                //enemy.AddStatusEffect(status, false, 0.3f);
                //enemy.CancelCast();
            }

            source.ApplyCooldownForAttackAction();

            map.SendVisualEffectToPlayers(DataManager.EffectIdForName["HammerFall"], position, 0);

            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(source.Character, position, CharacterSkill.HammerFall, lvl);
        }
    }
}