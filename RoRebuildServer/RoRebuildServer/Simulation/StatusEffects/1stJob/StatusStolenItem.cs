using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.StolenFrom, StatusClientVisibility.None, StatusEffectFlags.NoSave)]
    public class StatusStolenItem : StatusEffectBase
    {
        //nothing, it doesn't do anything
    }
}