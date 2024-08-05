using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    public class StatusTwoHandQuicken : StatusEffectBase
    {
        public override float Duration => 180f;
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.None;
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddStat(CharacterStat.AspdBonus, 30);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubStat(CharacterStat.AspdBonus, 30);
        }
    }
}
