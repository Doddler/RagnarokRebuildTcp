using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.TwoHandQuicken, StatusClientVisibility.Everyone)]
    public class StatusTwoHandQuicken : StatusEffectBase
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


            if (!DataManager.WeaponInfo.TryGetValue(weapon, out var weaponInfo) || !weaponInfo.IsTwoHanded)
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
}
