using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.Misc;

[StatusEffectHandler(CharacterStatusEffect.SpecialTarget, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave)]
public class SpecialTargetStatus : StatusEffectBase
{
    //nothing!
}