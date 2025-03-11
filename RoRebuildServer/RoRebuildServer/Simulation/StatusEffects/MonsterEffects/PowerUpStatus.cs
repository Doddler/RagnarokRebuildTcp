using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.PowerUp, StatusClientVisibility.Everyone)]
public class PowerUpStatus : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnDealDamage;

    public override StatusUpdateResult OnAttack(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.IsDamageResult && info.Flags.HasFlag(DamageApplicationFlags.PhysicalDamage))
        {
            if (!info.Target.TryGet<CombatEntity>(out var target))
                return StatusUpdateResult.Continue;

            var ratio = 1 + state.Value1 * 0.2f;
            //if (info.AttackSkill != CharacterSkill.None)
            //    ratio *= 2; //since power up is additive we want to add just a little more oomph to skills

            var attack = new AttackRequest(CharacterSkill.PowerUp, ratio, 1,
                AttackFlags.Physical | AttackFlags.IgnoreEvasion | AttackFlags.NoTriggers | AttackFlags.IgnoreNullifyingGroundMagic , AttackElement.None);

            if (info.Result == AttackResult.CriticalDamage)
                attack.Flags |= AttackFlags.GuaranteeCrit;
            else
                attack.Flags |= AttackFlags.IgnoreSubDefense;

            var res = ch.CalculateCombatResult(target, attack);

            if (res.Damage > 0)
                info.Damage += res.Damage; // / info.HitCount;
        }

        return StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.AddStat(CharacterStat.AddHit, 50 + state.Value1 * 10);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AddHit, 50 + state.Value1 * 10);
    }
}