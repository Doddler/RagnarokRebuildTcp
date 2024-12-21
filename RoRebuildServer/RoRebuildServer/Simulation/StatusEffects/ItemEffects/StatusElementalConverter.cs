using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.ItemEffects
{
    [StatusEffectHandler(CharacterStatusEffect.ElementalConverter, StatusClientVisibility.Owner)]
    public class StatusElementalConverter : StatusEffectBase
    {
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnChangeEquipment;

        public override StatusUpdateResult OnChangeEquipment(CombatEntity ch, ref StatusEffectState state)
        {
            if (ch.Character.Type != CharacterType.Player)
                return StatusUpdateResult.Continue;

            if (ch.Player.GetItemIdForEquipSlot(EquipSlot.Weapon) != state.Value2)
                return StatusUpdateResult.EndStatus;

            return StatusUpdateResult.Continue;
        }

        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SetStat(CharacterStat.EndowAttackElement, state.Value1);
            if (ch.Character.Type == CharacterType.Player)
                state.Value2 = ch.Player.GetItemIdForEquipSlot(EquipSlot.Weapon);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            if(ch.GetStat(CharacterStat.EndowAttackElement) == state.Value1)
                ch.SetStat(CharacterStat.EndowAttackElement, 0);
        }
    }
}
