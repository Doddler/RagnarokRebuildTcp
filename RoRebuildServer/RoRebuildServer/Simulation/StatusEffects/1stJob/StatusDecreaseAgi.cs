using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.DecreaseAgi, StatusClientVisibility.Everyone, StatusEffectFlags.None, "SpeedBonus")]
    public class StatusDecreaseAgi : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            if (state.Value2 == 0)
                state.Value2 = 25;

            ch.AddStat(CharacterStat.AddAgi, -(state.Value1 + 2));
            ch.AddStat(CharacterStat.MoveSpeedBonus, -state.Value2);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubStat(CharacterStat.AddAgi, -(state.Value1 + 2));
            ch.SubStat(CharacterStat.MoveSpeedBonus, -state.Value2);
        }
    }
}