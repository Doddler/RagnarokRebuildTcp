using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

//power thrust is split into two status effects so that blacksmiths won't overwrite their own self buffs
//and they can have different visual effects (persistent aura is only on the self version)
[StatusEffectHandler(CharacterStatusEffect.PowerThrustSelf, StatusClientVisibility.Everyone)]
[StatusEffectHandler(CharacterStatusEffect.PowerThrustParty, StatusClientVisibility.Everyone)]
public class StatusPowerThrust : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnPreCalculateDamageDealt;

    public override StatusUpdateResult OnPreCalculateDamage(CombatEntity ch, CombatEntity? target, ref StatusEffectState state,
        ref AttackRequest req)
    {
        if (target == null || (req.Flags & AttackFlags.Physical) == 0 || (req.Flags & AttackFlags.NoDamageModifiers) != 0)
            return StatusUpdateResult.Continue;

        req.AttackMultiplier += state.Value1 / 100f;

        return StatusUpdateResult.Continue;
    }
}
