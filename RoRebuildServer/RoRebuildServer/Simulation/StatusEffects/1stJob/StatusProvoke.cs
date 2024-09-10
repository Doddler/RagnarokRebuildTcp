using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

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

        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddStat(CharacterStat.AddDefPercent, -state.Value1 * 3);
            ch.AddStat(CharacterStat.AddAttackPercent, state.Value1 * 2);
            ch.AddStat(CharacterStat.AddMagicAttackPercent, state.Value1 * 2);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubStat(CharacterStat.AddDefPercent, -state.Value1 * 3);
            ch.SubStat(CharacterStat.AddAttackPercent, state.Value1 * 2);
            ch.SubStat(CharacterStat.AddMagicAttackPercent, state.Value1 * 2);
        }
    }
}
