using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs
{
    [StatusEffectHandler(CharacterStatusEffect.Blind, StatusClientVisibility.Everyone)]
    public class StatusBlind : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            var fleeDown = ch.GetEffectiveStat(CharacterStat.Agi) / 4;
            var hitDown = ch.GetEffectiveStat(CharacterStat.Dex) / 4;

            state.Value1 = fleeDown;
            state.Value2 = hitDown;

            ch.SubStat(CharacterStat.AddFlee, state.Value1);
            ch.SubStat(CharacterStat.AddHit, state.Value2);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddStat(CharacterStat.AddFlee, state.Value1);
            ch.AddStat(CharacterStat.AddHit, state.Value2);
        }
    }
}
