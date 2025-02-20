using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs;

[StatusEffectHandler(CharacterStatusEffect.Petrifying, StatusClientVisibility.Everyone, StatusEffectFlags.None, "Petrify")]
public class StatusPetrifying : StatusEffectBase
{
    public override StatusUpdateResult OnUpdateTick(CombatEntity ch, ref StatusEffectState state)
    {
        if (state.Value4 >= 5)
        {
            OnExpiration(ch, ref state); //the stone status will want these stats to be reset

            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Stone, 20);
            ch.AddStatusEffect(status, false);

            return StatusUpdateResult.EndStatus;
        }

        state.Value4++;

        ch.AddStat(CharacterStat.AddDex, -state.Value1);
        ch.AddStat(CharacterStat.AddAgi, -state.Value2);
        ch.AddStat(CharacterStat.AspdBonus, -state.Value3);
        ch.AddStat(CharacterStat.MoveSpeedBonus, -state.Value3);
        ch.ModifyExistingCastTime(1.2f);

        return StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        state.Value4++;
        state.Value1 = ch.GetEffectiveStat(CharacterStat.Dex) / 5;
        state.Value2 = ch.GetEffectiveStat(CharacterStat.Agi) / 5;
        state.Value3 = 20;

        ch.AddStat(CharacterStat.AddDex, -state.Value1 * state.Value4);
        ch.AddStat(CharacterStat.AddAgi, -state.Value2 * state.Value4);
        ch.AddStat(CharacterStat.AspdBonus, -state.Value3 * state.Value4);
        ch.AddStat(CharacterStat.MoveSpeedBonus, -state.Value3 * state.Value4);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        if (state.Value4 == 0)
            return;

        ch.SubStat(CharacterStat.AddDex, state.Value1 * state.Value4);
        ch.SubStat(CharacterStat.AddAgi, state.Value2 * state.Value4);
        ch.SubStat(CharacterStat.AspdBonus, state.Value3 * state.Value4);
        ch.SubStat(CharacterStat.MoveSpeedBonus, state.Value3 * state.Value4);
        //make sure we don't reverse the stat changes more than once
        state.Value1 = 0;
        state.Value2 = 0;
        state.Value3 = 0;
        state.Value4 = 0;
    }
}