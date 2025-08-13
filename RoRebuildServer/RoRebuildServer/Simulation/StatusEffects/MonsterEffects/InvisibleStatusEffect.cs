using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.Invisible, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave, "Hiding")]
public class InvisibleStatusEffect : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.State != CharacterState.Dead)
            ch.Character.State = CharacterState.Idle;

        ch.SetBodyState(BodyStateFlags.Cloaking);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.RemoveBodyState(BodyStateFlags.Cloaking);
    }
}