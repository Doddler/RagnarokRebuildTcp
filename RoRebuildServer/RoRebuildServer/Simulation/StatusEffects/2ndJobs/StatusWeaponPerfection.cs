using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using System.Runtime.CompilerServices;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.WeaponPerfection, StatusClientVisibility.Ally)]
public class StatusWeaponPerfection : StatusEffectBase
{
    //why in the world did I make it so complicated...?

    public override StatusUpdateResult OnChangeEquipment(CombatEntity ch, ref StatusEffectState state)
    {
        OnExpiration(ch, ref state);
        OnApply(ch, ref state);

        return StatusUpdateResult.Continue;
    }

    private (int, int, int) GetSizeModifierForWeaponClass(WeaponClass type, int power)
    {

        var weak = power - (power * 25 / 100);

        switch (type)
        {
            case WeaponClass.Dagger:
            case WeaponClass.Rod:
            case WeaponClass.TwoHandRod:
            case WeaponClass.Pistol:
            case WeaponClass.Rifle:
            case WeaponClass.Shotgun:
            case WeaponClass.GatlingGun:
            case WeaponClass.Grenade:
                return (power, weak, weak);
            case WeaponClass.Sword:
            case WeaponClass.Mace:
            case WeaponClass.Katar:
            case WeaponClass.Knuckle:
            case WeaponClass.Bow:
            case WeaponClass.Book:
            case WeaponClass.Instrument:
            case WeaponClass.Whip:
                return (weak, power, weak);
            case WeaponClass.TwoHandSword:
            case WeaponClass.Spear:
            case WeaponClass.TwoHandSpear:
            case WeaponClass.Axe:
            case WeaponClass.TwoHandAxe:
            case WeaponClass.Shuriken:
                return (weak, weak, power);
            default:
                return (weak, weak, weak);
        }
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        state.Value4 = (byte)(ch.Character.Type == CharacterType.Player ? (WeaponClass)ch.Player.MainWeaponClass : WeaponClass.None);

        var (small, medium, large) = GetSizeModifierForWeaponClass((WeaponClass)state.Value4, state.Value1);

        ch.AddStat(CharacterStat.IgnoreDefSmall, small);
        ch.AddStat(CharacterStat.IgnoreDefMedium, medium);
        ch.AddStat(CharacterStat.IgnoreDefLarge, large);
        ch.AddStat(CharacterStat.AddCritDamageSmall, small);
        ch.AddStat(CharacterStat.AddCritDamageMedium, medium);
        ch.AddStat(CharacterStat.AddCritDamageLarge, large);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        var (small, medium, large) = GetSizeModifierForWeaponClass((WeaponClass)state.Value4, state.Value1);

        ch.SubStat(CharacterStat.IgnoreDefSmall, small);
        ch.SubStat(CharacterStat.IgnoreDefMedium, medium);
        ch.SubStat(CharacterStat.IgnoreDefLarge, large);
        ch.SubStat(CharacterStat.AddCritDamageSmall, small);
        ch.SubStat(CharacterStat.AddCritDamageMedium, medium);
        ch.SubStat(CharacterStat.AddCritDamageLarge, large);
    }
}
