using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs;

[StatusEffectHandler(CharacterStatusEffect.Silence, StatusClientVisibility.Everyone)]
public class StatusSilence : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SetBodyState(BodyStateFlags.Silence);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.RemoveBodyState(BodyStateFlags.Silence);
    }
}