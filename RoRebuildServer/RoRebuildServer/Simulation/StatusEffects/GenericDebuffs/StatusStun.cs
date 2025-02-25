using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs
{
    [StatusEffectHandler(CharacterStatusEffect.Stun, StatusClientVisibility.Everyone)]
    public class StatusStun : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddDisabledState();
            ch.SetBodyState(BodyStateFlags.Stunned);
            ch.SubStat(CharacterStat.AddFlee, 999);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubDisabledState();
            ch.RemoveBodyState(BodyStateFlags.Stunned);
            ch.AddStat(CharacterStat.AddFlee, 999);
        }
    }
}
