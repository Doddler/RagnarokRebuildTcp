using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.ImproveConcentration, StatusClientVisibility.Owner)]
    public class StatusImproveConcentration : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddStat(CharacterStat.AddAgi, state.Value1);
            ch.AddStat(CharacterStat.AddDex, state.Value2);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubStat(CharacterStat.AddAgi, state.Value1);
            ch.SubStat(CharacterStat.AddDex, state.Value2);
        }
    }
}
