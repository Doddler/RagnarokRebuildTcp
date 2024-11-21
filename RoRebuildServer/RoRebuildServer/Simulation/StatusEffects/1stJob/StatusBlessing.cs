using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.Blessing, StatusClientVisibility.Everyone)]
    public class StatusBlessing : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddStat(CharacterStat.AddStr, state.Value1);
            ch.AddStat(CharacterStat.AddInt, state.Value1);
            ch.AddStat(CharacterStat.AddDex, state.Value1);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubStat(CharacterStat.AddStr, state.Value1);
            ch.SubStat(CharacterStat.AddInt, state.Value1);
            ch.SubStat(CharacterStat.AddDex, state.Value1);
        }
    }
}
