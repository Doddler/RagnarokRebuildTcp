using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.Cloaking, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave, "Hiding")]
public class StatusCloaking : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnTakeDamage | StatusUpdateMode.OnMove | StatusUpdateMode.OnUpdate;

    //Value1: Skill level
    //Value2: Seconds between consuming SP
    //Value3: The amount move speed is modified by
    //Value4: If the player is against a wall

    public override StatusUpdateResult OnUpdateTick(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type != CharacterType.Player)
            return StatusUpdateResult.Continue;

        //OnUpdateTick is called once per second. We decrement the counter, and if we hit zero we take the SP and then reset it.
        state.Value2--;
        if (state.Value2 > 0)
            return StatusUpdateResult.Continue;

        if (!ch.Player.TryTakeSpValue(state.Value1 == 0 ? 2 : 1)) //consume 2 sp at level 1, 1 above that
            return StatusUpdateResult.EndStatus;

        state.Value2 += state.Value1 - 1; //levels 1 and 2 will both update every tick with this

        return StatusUpdateResult.Continue;
    }

    public override StatusUpdateResult OnMove(CombatEntity ch, ref StatusEffectState state, Position src, Position dest, bool isTeleport)
    {
        if (isTeleport)
            return StatusUpdateResult.EndStatus;

        if (ch.Character.Type == CharacterType.Monster)
            return StatusUpdateResult.Continue;

        UpdateWallBonus(ch, ref state);
        if (state.Value1 <= 2 && state.Value4 != 1)
            return StatusUpdateResult.EndStatus;

        return StatusUpdateResult.Continue;
    }

    public override StatusUpdateResult OnChangeMaps(CombatEntity ch, ref StatusEffectState state)
    {
        return StatusUpdateResult.EndStatus;
    }

    public override StatusUpdateResult OnTakeDamage(CombatEntity ch, ref StatusEffectState state, ref DamageInfo info)
    {
        if (info.Damage > 0)
            return StatusUpdateResult.EndStatus;

        return StatusUpdateResult.Continue;
    }

    private void UpdateWallBonus(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type == CharacterType.Monster)
            return;

        if (state.Value4 == 0 && (ch.Character.Map?.WalkData.IsCellAdjacentToWall(ch.Character.Position) ?? false))
        {
            state.Value4 = 1;
            state.Value3 += 30;
            ch.AddStat(CharacterStat.MoveSpeedBonus, 30);
            return;
        }

        if (state.Value4 == 1 && !(ch.Character.Map?.WalkData.IsCellAdjacentToWall(ch.Character.Position) ?? false))
        {
            state.Value4 = 0;
            state.Value3 -= 30;
            ch.SubStat(CharacterStat.MoveSpeedBonus, 30);
        }
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.State != CharacterState.Dead)
            ch.Character.State = CharacterState.Idle;

        ch.SetBodyState(BodyStateFlags.Cloaking);

        state.Value2 = state.Value1 - 1; //seconds between sp consumption ticks. Levels 1 and 2 update every tick.
        state.Value3 = (short)(-30 + state.Value1 * 3); //move speed change
        state.Value4 = 0;

        if (ch.Character.Type != CharacterType.Monster)
        {
            ch.AddStat(CharacterStat.MoveSpeedBonus, state.Value3);
            ch.AddStat(CharacterStat.AddSpRecoveryPercent, -100);

            UpdateWallBonus(ch, ref state);
        }
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.RemoveBodyState(BodyStateFlags.Cloaking);

        if (ch.Character.Type != CharacterType.Monster)
        {
            ch.SubStat(CharacterStat.MoveSpeedBonus, state.Value3); //wall bonus should be lumped into this
            ch.SubStat(CharacterStat.AddSpRecoveryPercent, -100);
        }
    }
}