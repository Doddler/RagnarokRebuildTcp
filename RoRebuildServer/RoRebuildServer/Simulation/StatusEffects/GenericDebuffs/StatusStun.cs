using RebuildSharedData.Enum;
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
            ch.SubStat(CharacterStat.AddFlee, 999);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.SubDisabledState();
            ch.AddStat(CharacterStat.AddFlee, 999);
        }
    }
}
