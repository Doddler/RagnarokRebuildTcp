using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.TwoHandQuicken, StatusClientVisibility.Everyone)]
    public class StatusTwoHandQuicken : StatusEffectBase
    {
        public override float Duration => 180f; //unused??
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.None;
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddStat(CharacterStat.AspdBonus, state.Value1);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubStat(CharacterStat.AspdBonus, state.Value1);
        }
    }
}
