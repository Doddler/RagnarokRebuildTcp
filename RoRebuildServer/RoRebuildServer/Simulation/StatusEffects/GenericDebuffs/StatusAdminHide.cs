using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs;

[StatusEffectHandler(CharacterStatusEffect.AdminHide, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave)]
public class StatusAdminHide : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type != CharacterType.Player)
            return;
        ch.Character.AdminHidden = true;
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type != CharacterType.Player)
            return;
        ch.Character.AdminHidden = false;
    }
}