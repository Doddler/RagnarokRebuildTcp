using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.BlitzBeatRateUp, StatusClientVisibility.None, StatusEffectFlags.NoSave)]
public class StatusBlitzBeatRateUp : StatusEffectBase
{
}
