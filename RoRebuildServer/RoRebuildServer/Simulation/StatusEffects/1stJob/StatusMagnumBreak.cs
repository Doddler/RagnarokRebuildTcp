using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.MagnumBreak, StatusClientVisibility.Owner, StatusEffectFlags.NoSave)]
public class StatusMagnumBreak : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnDealDamage;

    public override StatusUpdateResult OnAttack(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.IsDamageResult && info.Flags.HasFlag(DamageApplicationFlags.PhysicalDamage))
        {
            if (!info.Target.TryGet<CombatEntity>(out var target))
                return StatusUpdateResult.Continue;

            var attack = new AttackRequest(CharacterSkill.MagnumBreak, 1f, 1, 
                AttackFlags.Physical | AttackFlags.IgnoreEvasion | AttackFlags.NoTriggers, AttackElement.Fire);

            if (info.Result == AttackResult.CriticalDamage)
                attack.Flags |= AttackFlags.GuaranteeCrit;
            else
                attack.Flags |= AttackFlags.IgnoreSubDefense;

            var res = ch.CalculateCombatResult(target, attack);

            if (res.Damage > 0)
                info.Damage += res.Damage * (10 + state.Value1) / 100;
        }

        return StatusUpdateResult.Continue;
    }
}