using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs
{
    [StatusEffectHandler(CharacterStatusEffect.VenomSplasher, StatusClientVisibility.None, StatusEffectFlags.NoSave)]
    public class StatusVenomSplasher : StatusEffectBase
    {
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnTakeDamage;

        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            //var attackerEntity = World.Instance.GetEntityById(state.Value1);
            //if (!attackerEntity.TryGet<CombatEntity>(out var attacker))
            //    return;

            //var (_, atk) = attacker.CalculateAttackPowerRange(false);
            //if (state.Value2 > short.MaxValue)
            //    state.Value2 = short.MaxValue;
        }

        public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
        {
            if (!info.IsDamageResult)
                return StatusUpdateResult.Continue;

            if ((info.Flags & DamageApplicationFlags.PhysicalDamage) <= 0)
                return StatusUpdateResult.Continue;

            var totalDamage = state.Value3 + info.Damage * info.HitCount;
            state.Value3 = (short)totalDamage;
            if (totalDamage >= state.Value2 || totalDamage > short.MaxValue)
                return StatusUpdateResult.EndStatus;

            var eleMod = ch.GetElementalReductionForReceivedAttack(null, AttackElement.Poison);

            if (totalDamage * eleMod / 100 > ch.GetStat(CharacterStat.Hp))
                return StatusUpdateResult.EndStatus;

            return StatusUpdateResult.Continue;
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            var attackerEntity = World.Instance.GetEntityById(state.Value1);
            var map = ch.Character.Map;
            if (!attackerEntity.TryGet<CombatEntity>(out var attacker) || map == null || state.Value3 <= 0)
                return;

            map.AddVisiblePlayersAsPacketRecipients(attacker.Character, ch.Character); //combines src and target visibility lists

            using var targetList = EntityListPool.Get();
            map.GatherEnemiesInArea(attacker.Character, ch.Character.Position, 2, targetList, true, true);

            var attackReq = new AttackRequest(CharacterSkill.VenomSplasherDetonation, 1, 1,
                 AttackFlags.IgnoreDefense | AttackFlags.IgnoreEvasion | AttackFlags.NoDamageModifiers, AttackElement.Poison);
            var atk = (int)state.Value3;
            if (atk <= 0)
                atk = 1;
            if (atk > state.Value2)
                atk = state.Value2;
            attackReq.MinAtk = attackReq.MaxAtk = atk;
            
            var res = attacker.CalculateCombatResult(ch, attackReq);
            res.IsIndirect = true;
            res.Time = 0;
            attacker.ExecuteCombatResult(res, false);
            CommandBuilder.SkillExecuteTargetedSkill(attacker.Character, ch.Character, CharacterSkill.VenomSplasherDetonation, 1, res);

            attackReq.AttackMultiplier = 2 / 3f;
            
            foreach (var e in targetList)
            {
                if (!e.TryGet<WorldObject>(out var blastTarget) || blastTarget == ch.Character)
                    continue;

                res = attacker.CalculateCombatResult(e.Get<CombatEntity>(), attackReq);
                res.IsIndirect = true;
                res.Time = 0;
                attacker.ExecuteCombatResult(res, false);

                CommandBuilder.AttackMulti(attacker.Character, blastTarget, res, false);
            }

            CommandBuilder.ClearRecipients();
        }
    }
}
