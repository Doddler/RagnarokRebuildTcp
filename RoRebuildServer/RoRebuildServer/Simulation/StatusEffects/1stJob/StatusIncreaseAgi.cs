using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.IncreaseAgi, StatusClientVisibility.Everyone)]
    public class StatusIncreaseAgi : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            var lvl = state.Value1;
            state.Value1 = 2 + lvl; //agi bonus
            state.Value2 = 25; //will cap out player move speed buff
            if (lvl > 10)
                state.Value2 = 100;
            ch.AddStat(CharacterStat.AddAgi, state.Value1);
            ch.AddStat(CharacterStat.MoveSpeedBonus, state.Value2);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubStat(CharacterStat.AddAgi, state.Value1);
            ch.SubStat(CharacterStat.MoveSpeedBonus, state.Value2);
        }
    }
}
