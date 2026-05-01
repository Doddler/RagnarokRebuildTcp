using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.PowerMaximize, StatusClientVisibility.Everyone)]
public class StatusPowerMaximize : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnPreCalculateDamageDealt | StatusUpdateMode.OnUpdate;

    public override StatusUpdateResult OnUpdateTick(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type != CharacterType.Player)
            return StatusUpdateResult.Continue;

        state.Value2++;
        if (state.Value2 > state.Value1)
        {
            if (!ch.Player.TryTakeSpValue(1))
                return StatusUpdateResult.EndStatus;
            state.Value2 = 0;
        }

        return StatusUpdateResult.Continue;
    }

    public override StatusUpdateResult OnPreCalculateDamage(CombatEntity ch, CombatEntity? target, ref StatusEffectState state, ref AttackRequest req)
    {
        if ((req.Flags & AttackFlags.Physical) > 0 && (req.Flags & AttackFlags.NoDamageModifiers) == 0)
            req.MinAtk = req.MaxAtk;

        return StatusUpdateResult.Continue;
    }
}
