using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.KyrieEleison, StatusClientVisibility.Ally)]
public class StatusKyrieEleison : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnCalculateDamageTaken;

    public override StatusUpdateResult OnCalculateDamage(CombatEntity ch, ref StatusEffectState state, ref AttackRequest req,
        ref DamageInfo info)
    {
        if (!info.IsDamageResult)
            return StatusUpdateResult.Continue;

        var totalDamage = info.Damage * info.HitCount;
        var remaining = state.Value1 - totalDamage;

        if (remaining >= 0)
        {
            state.Value1 = remaining;
            state.Value2--;
            info.Result = AttackResult.Block;
            info.Damage = 0;

            if (remaining == 0 || state.Value2 <= 0)
                return StatusUpdateResult.EndStatus;

            return StatusUpdateResult.Continue;
        }

        var over = (-remaining) / info.HitCount;
        info.Damage = over;
        return StatusUpdateResult.EndStatus;
    }
}