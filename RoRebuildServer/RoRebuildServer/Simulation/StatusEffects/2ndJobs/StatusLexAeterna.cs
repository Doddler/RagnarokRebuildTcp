using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.LexAeterna, StatusClientVisibility.Owner)]
public class StatusLexAeterna : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnCalculateDamageTaken;

    public override StatusUpdateResult OnCalculateDamage(CombatEntity ch, ref StatusEffectState state, ref AttackRequest req,
        ref DamageInfo info)
    {
        if (info.IsDamageResult)
        {
            info.Damage *= 2;
            return StatusUpdateResult.EndStatus;
        }

        return StatusUpdateResult.Continue;
    }
}