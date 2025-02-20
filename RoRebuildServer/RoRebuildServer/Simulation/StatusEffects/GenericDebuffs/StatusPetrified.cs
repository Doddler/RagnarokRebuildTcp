using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs;

[StatusEffectHandler(CharacterStatusEffect.Stone, StatusClientVisibility.Everyone, StatusEffectFlags.None, "Petrify")]
public class StatusPetrified : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnTakeDamage;

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.AddStat(CharacterStat.AddDefPercent, -50);
        ch.AddStat(CharacterStat.AddMDefPercent, 25);
        ch.AddStat(CharacterStat.AddFlee, -999);
        ch.SetStat(CharacterStat.OverrideElement, (int)CharacterElement.Earth2);

        ch.SetBodyState(BodyStateFlags.Petrified);
        ch.Character.StopMovingImmediately();
        ch.AddDisabledState();
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AddDefPercent, -50);
        ch.SubStat(CharacterStat.AddMDefPercent, 25);
        ch.SubStat(CharacterStat.AddFlee, -999);
        ch.SetStat(CharacterStat.OverrideElement, (int)CharacterElement.None);

        ch.RemoveBodyState(BodyStateFlags.Petrified);
        ch.SubDisabledState();
    }

    public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.IsDamageResult && info.Damage > 0)
            return StatusUpdateResult.EndStatus;
        return StatusUpdateResult.Continue;
    }
}