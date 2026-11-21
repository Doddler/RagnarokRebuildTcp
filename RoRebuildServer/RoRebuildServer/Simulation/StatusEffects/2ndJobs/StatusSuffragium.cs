using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.Suffragium, StatusClientVisibility.Party)]
public class StatusSuffragium : StatusEffectBase
{
    //handled in SkillHandler (but probably shouldn't be)
}