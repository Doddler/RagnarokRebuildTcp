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
    //public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnDealDamage;
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnPreCalculateDamageDealt;

    public override StatusUpdateResult OnPreCalculateDamage(CombatEntity ch, CombatEntity? target, ref StatusEffectState state,
        ref AttackRequest req)
    {
        if (target == null || (req.Flags & AttackFlags.Physical) == 0 || (req.Flags & AttackFlags.NoDamageModifiers) != 0)
            return StatusUpdateResult.Continue;

        req.AttackMultiplier += 1 + state.Value1 * 0.2f;

        return StatusUpdateResult.Continue;
    }

    //public override StatusUpdateResult OnAttack(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    //{
    //    if (info.IsDamageResult && info.Flags.HasFlag(DamageApplicationFlags.PhysicalDamage))
    //    {
    //        if (!info.Target.TryGet<CombatEntity>(out var target))
    //            return StatusUpdateResult.Continue;

    //        var ratio = 1 + state.Value1 * 0.2f;

    //        var attack = new AttackRequest(CharacterSkill.PowerUp, ratio, 1,
    //            AttackFlags.Physical | AttackFlags.IgnoreEvasion | AttackFlags.NoTriggers | AttackFlags.IgnoreNullifyingGroundMagic, AttackElement.None);

    //        if (info.Result == AttackResult.CriticalDamage)
    //            attack.Flags |= AttackFlags.GuaranteeCrit;
    //        else
    //            attack.Flags |= AttackFlags.IgnoreSubDefense;

    //        var res = ch.CalculateCombatResult(target, attack);

    //        if (res.Damage > 0)
    //            info.Damage += res.Damage; // / info.HitCount;
    //    }

    //    return StatusUpdateResult.Continue;
    //}

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.AddStat(CharacterStat.AddHit, 25 + state.Value1 * 10);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AddHit, 25 + state.Value1 * 10);
    }
}