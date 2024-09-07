using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.PushCart, StatusClientVisibility.Everyone, StatusEffectFlags.StayOnClear)]
public class StatusPushCart : StatusEffectBase
{
    //for now it does nothing
}