using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.AdrenalineRush, StatusClientVisibility.Owner, StatusEffectFlags.None, "AspdBoost")]
public class StatusAdrenalineRush : StatusEffectBase
{
    //State Value 1 : Attack Speed Bonus
    //State Value 2 : >0 to ignore weapon class requirement

    public override float Duration => 180f; //default but probably unused
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnChangeEquipment;

    public override StatusUpdateResult OnChangeEquipment(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type != CharacterType.Player || state.Value2 > 0)
            return StatusUpdateResult.Continue;

        var weapon = ch.Player.GetItemIdForEquipSlot(EquipSlot.Weapon);
        if (weapon < 0)
            return StatusUpdateResult.EndStatus;

        if (!DataManager.WeaponInfo.TryGetValue(weapon, out var weaponInfo))
            return StatusUpdateResult.EndStatus;

        //useable only with 1h axe, 2h axe, 1h mace, 2h mace
        if (weaponInfo.WeaponClass < 6 || weaponInfo.WeaponClass > 9) 
            return StatusUpdateResult.EndStatus;

        return StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.AddStat(CharacterStat.AspdBonus, state.Value1);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AspdBonus, state.Value1);
    }
}
