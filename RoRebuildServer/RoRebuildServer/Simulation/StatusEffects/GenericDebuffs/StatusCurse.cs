using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs
{
    [StatusEffectHandler(CharacterStatusEffect.Curse, StatusClientVisibility.Everyone)]
    public class StatusCurse : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            //move speed is handled as a special case in update stats for players and monsters
            state.Value1 = ch.GetStat(CharacterStat.Luck);
            ch.SubStat(CharacterStat.AddLuk, state.Value1);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddStat(CharacterStat.AddLuk, state.Value1);
        }
    }
}
