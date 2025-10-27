using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.Magnificat, StatusClientVisibility.Ally)]
public class StatusMagnificat : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.AddStat(CharacterStat.AddSpRecoveryPercent, 100);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AddSpRecoveryPercent, 100);
    }
}