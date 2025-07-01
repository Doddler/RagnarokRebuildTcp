using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.AnkleSnare, StatusClientVisibility.None, StatusEffectFlags.NoSave)]
public class AnkleSnareStatus : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SetBodyState(BodyStateFlags.Snared);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.RemoveBodyState(BodyStateFlags.Snared);
    }
}