using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs;

[StatusEffectHandler(CharacterStatusEffect.Stone, StatusClientVisibility.Everyone, StatusEffectFlags.None, "Petrify")]
public class StatusPetrified : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnTakeDamage | StatusUpdateMode.OnCalculateDamageTaken;

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

        state.Value4 = 0;
    }

    //we only want to proc the +50% damage once, but we don't want them to be free from stone until a hit actually lands,
    //so we store a flag to make sure we don't add bonus damage to more than one hit before the damage is applied.
    public override StatusUpdateResult OnCalculateDamage(CombatEntity ch, ref StatusEffectState state, ref AttackRequest req,
        ref DamageInfo info)
    {
        if (state.Value4 == 0 && info.IsDamageResult && info.Damage > 0)
        {
            info.Damage = info.Damage * 150 / 100;
            state.Value4 = 1;
        }

        return StatusUpdateResult.Continue;
    }

    public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (state.Value4 == 1) //(info.IsDamageResult && info.Damage > 0) || 
            return StatusUpdateResult.EndStatus;

        return StatusUpdateResult.Continue;
    }
}