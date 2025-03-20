using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.SignumCrusis, StatusClientVisibility.None)]
public class StatusSignumCrusis : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        state.Value2 = ch.GetStat(CharacterStat.Def) * state.Value1 / 100;
        ch.AddStat(CharacterStat.AddDef, -state.Value2);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AddDef, -state.Value2);
    }
}