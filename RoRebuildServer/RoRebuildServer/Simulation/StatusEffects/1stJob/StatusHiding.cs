using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.Hiding, StatusClientVisibility.Everyone)]
public class StatusHiding : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnTakeDamage | StatusUpdateMode.OnUpdate;

    public override StatusUpdateResult OnUpdateTick(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type != CharacterType.Player)
            return StatusUpdateResult.Continue;
        
        state.Value2--;
        if (state.Value2 > 0)
            return StatusUpdateResult.Continue;

        if(!ch.Player.TryTakeSpValue(1))
            return StatusUpdateResult.EndStatus;

        state.Value2 += state.Value1;

        return StatusUpdateResult.Continue;
    }

    public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (ch.GetSpecialType() == CharacterSpecialType.Boss && info.AttackSkill != CharacterSkill.Ruwach)
            return StatusUpdateResult.Continue;

        if (info.Damage > 0)
            return StatusUpdateResult.EndStatus;

        return StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.State != CharacterState.Dead)
            ch.Character.State = CharacterState.Idle;
        ch.SetBodyState(BodyStateFlags.Hidden);
        ch.AddStat(CharacterStat.AddSpRecoveryPercent, -50);
        state.Value2 = state.Value1;
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.RemoveBodyState(BodyStateFlags.Hidden);
        ch.SubStat(CharacterStat.AddSpRecoveryPercent, -50);
    }
}