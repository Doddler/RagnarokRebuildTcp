using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.Bulwark, StatusClientVisibility.Everyone)]
public class BulwarkStatus : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        var def = ch.GetEffectiveStat(CharacterStat.Def);
        var mdef = ch.GetEffectiveStat(CharacterStat.MDef);

        state.Value1 = 150 - def; //add enough def to reach 120
        state.Value2 = mdef;

        ch.AddStat(CharacterStat.Def, state.Value1);
        ch.AddStat(CharacterStat.MDef, state.Value2);

        ch.AddDisabledState();
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.Def, state.Value1);
        ch.SubStat(CharacterStat.MDef, state.Value2);

        ch.SubDisabledState();
    }
}