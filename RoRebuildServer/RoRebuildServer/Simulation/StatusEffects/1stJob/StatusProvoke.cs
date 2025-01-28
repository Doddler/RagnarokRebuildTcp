using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RebuildSharedData.Enum.EntityStats;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.Provoke, StatusClientVisibility.None, StatusEffectFlags.NoSave)]
    public class StatusProvoke : StatusEffectBase
    {
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnDealDamage;

        public override StatusUpdateResult OnAttack(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
        {
            if (!info.Target.TryGet<CombatEntity>(out var target))
                return StatusUpdateResult.Continue;

            //if our current target matches the one that provoked this character, add a chance to do big damage
            if (info.Result == AttackResult.NormalDamage && target.Character.Id == state.Value2)
            {
                if(ch.CheckLuckModifiedRandomChanceVsTarget(target, 10, 1000))
                {
                    info.Damage *= 2;
                    info.Result = AttackResult.CriticalDamage;
                }
            }

            return StatusUpdateResult.Continue;
        }

        private (int, int, int) GetProvokeProperties(CombatEntity ch, int lvl)
        {
            var defMod = -(5 + lvl * 5);
            var atkMod = 2 + lvl * 3;
            var matkMod = lvl * 2;
            if (ch.GetSpecialType() == CharacterSpecialType.Boss)
            {
                defMod /= 2;
                atkMod /= 2;
                matkMod /= 2;
            }
            return (defMod, atkMod, matkMod);
        }

        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            var (defMod, atkMod, matkMod) = GetProvokeProperties(ch, state.Value1);

            ch.AddStat(CharacterStat.AddDefPercent, defMod);
            ch.AddStat(CharacterStat.AddAttackPercent, atkMod);
            ch.AddStat(CharacterStat.AddMagicAttackPercent, matkMod);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            var (defMod, atkMod, matkMod) = GetProvokeProperties(ch, state.Value1);

            ch.SubStat(CharacterStat.AddDefPercent, defMod);
            ch.SubStat(CharacterStat.AddAttackPercent, atkMod);
            ch.SubStat(CharacterStat.AddMagicAttackPercent, matkMod);
        }
    }
}
