using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs;

[StatusEffectHandler(CharacterStatusEffect.Sleep, StatusClientVisibility.Everyone)]
public class StatusSleep : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnTakeDamage;

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.AddDisabledState();
        ch.SetBodyState(BodyStateFlags.Sleep);
        ch.SubStat(CharacterStat.AddFlee, 999);
        ch.Character.StopMovingImmediately();
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubDisabledState();
        ch.RemoveBodyState(BodyStateFlags.Sleep);
        ch.AddStat(CharacterStat.AddFlee, 999);
    }

    public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.IsDamageResult && info.Damage > 0)
            return StatusUpdateResult.EndStatus;
        return StatusUpdateResult.Continue;
    }
}