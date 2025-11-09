using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;


[StatusEffectHandler(CharacterStatusEffect.PecoRiding, StatusClientVisibility.Owner, StatusEffectFlags.NoSave | StatusEffectFlags.StayOnClear)]
public class StatusPecoRiding : StatusEffectBase
{

}