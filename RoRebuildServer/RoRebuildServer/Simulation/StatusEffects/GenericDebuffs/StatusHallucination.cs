using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs;

[StatusEffectHandler(CharacterStatusEffect.Hallucination, StatusClientVisibility.Owner)]
public class StatusHallucination : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SetBodyState(BodyStateFlags.Hallucination);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.RemoveBodyState(BodyStateFlags.Hallucination);
    }
}