using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

//Player.FollowersChanged adds and removes it with the peco
[StatusEffectHandler(CharacterStatusEffect.PecoRiding, StatusClientVisibility.Owner, StatusEffectFlags.NoSave | StatusEffectFlags.StayOnClear)]
public class StatusPecoRiding : StatusEffectBase
{
}