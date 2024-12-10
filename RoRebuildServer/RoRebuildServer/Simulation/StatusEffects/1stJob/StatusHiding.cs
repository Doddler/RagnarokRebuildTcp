using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.Hiding, StatusClientVisibility.Everyone)]
public class StatusHiding : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.State != CharacterState.Dead)
            ch.Character.State = CharacterState.Idle;
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.State == CharacterState.Hide)
            ch.Character.State = CharacterState.Idle;
    }
}