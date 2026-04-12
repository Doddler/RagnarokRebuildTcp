using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Util;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.Provoke, StatusClientVisibility.Owner, StatusEffectFlags.NoSave)]
    public class StatusProvoke : StatusEffectBase
    {
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnDealDamage | StatusUpdateMode.OnPreCalculateDamageDealt;

        public override StatusUpdateResult OnPreCalculateDamage(CombatEntity ch, CombatEntity? target, ref StatusEffectState state, ref AttackRequest req)
        {
            var boost = ch.GetSpecialType() == CharacterSpecialType.Boss ? 1 : 2;
            if (target != null && ch.Character.Type != CharacterType.Player && target.Character.Id == state.Value2)
                boost *= 2;

            if (state.Value3 == 0 || state.Value1 < 10) //monster provoke level 10 doesn't boost attack (val 3 is a monster source, val1 is skill level)
            {
                req.MinAtk += req.MinAtk * (boost + state.Value1 * boost) / 100;
                req.MaxAtk += req.MaxAtk * (boost + state.Value1 * boost) / 100;
            }

            return StatusUpdateResult.Continue;
        }

        //public override StatusUpdateResult OnAttack(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
        //{
        //    if (state.Value3 > 0 || !info.Target.TryGet<CombatEntity>(out var target))
        //        return StatusUpdateResult.Continue;

        //    //if our current target matches the one that provoked this character, add a chance to do big damage
        //    if (info.Result == AttackResult.NormalDamage && target.Character.Id == state.Value2)
        //    {
        //        if (ch.CheckLuckModifiedRandomChanceVsTarget(target, 10, 1000))
        //        {
        //            info.Damage *= 2;
        //            info.Result = AttackResult.CriticalDamage;
        //        }
        //    }

        //    return StatusUpdateResult.Continue;
        //}

        private (int, int, int, int) GetProvokeProperties(CombatEntity ch, int lvl, bool isMonsterSource)
        {
            var defMod = -(5 + lvl * 5);
            var softDefMod = isMonsterSource && lvl >= 10 ? -100 : defMod; //level 10 from monsters only completely wipes out soft defense
            var atkMod = 2 + lvl * 3;
            var matkMod = lvl * 2;
            if (ch.GetSpecialType() == CharacterSpecialType.Boss)
            {
                defMod /= 2;
                softDefMod /= 2;
                atkMod /= 2;
                matkMod /= 2;
            }

            return (defMod, softDefMod, atkMod, matkMod);
        }

        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            var (defMod, softDefMod, atkMod, matkMod) = GetProvokeProperties(ch, state.Value1, state.Value3 > 0);

            ch.AddStat(CharacterStat.AddDefPercent, defMod);
            ch.AddStat(CharacterStat.AddSoftDefPercent, softDefMod);
            //ch.AddStat(CharacterStat.AddAttackPercent, atkMod);
            //ch.AddStat(CharacterStat.AddMagicAttackPercent, matkMod);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            var (defMod, softDefMod, atkMod, matkMod) = GetProvokeProperties(ch, state.Value1, state.Value3 > 0);

            ch.SubStat(CharacterStat.AddDefPercent, defMod);
            ch.SubStat(CharacterStat.AddSoftDefPercent, softDefMod);
            //ch.SubStat(CharacterStat.AddAttackPercent, atkMod);
            //ch.SubStat(CharacterStat.AddMagicAttackPercent, matkMod);
        }
    }
}