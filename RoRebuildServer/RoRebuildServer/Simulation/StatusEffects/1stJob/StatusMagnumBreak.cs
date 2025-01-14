using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.MagnumBreak, StatusClientVisibility.Owner, StatusEffectFlags.NoSave)]
public class StatusMagnumBreak : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnDealDamage;

    public override StatusUpdateResult OnAttack(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.IsDamageResult)
        {
            if (!info.Target.TryGet<CombatEntity>(out var target))
                return StatusUpdateResult.Continue;

            var bonus = info.Damage * (10 + state.Value1) / 100; //11% - 20%
            var eleMod = DataManager.ElementChart.GetAttackModifier(AttackElement.Fire, target.GetElement());
            var damage = bonus * eleMod / 100;
            if (damage > 0)
                info.Damage += damage;
        }

        return StatusUpdateResult.Continue;
    }
}