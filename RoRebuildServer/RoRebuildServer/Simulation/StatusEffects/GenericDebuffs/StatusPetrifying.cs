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
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnUpdate;

    public override StatusUpdateResult OnUpdateTick(CombatEntity ch, ref StatusEffectState state)
    {
        ch.ModifyExistingCastTime(1.5f);

        var subDex = ch.GetEffectiveStat(CharacterStat.Dex) / 2;
        var subAgi = ch.GetEffectiveStat(CharacterStat.Agi) / 2;
        var subSpeed = (100 + ch.GetEffectiveStat(CharacterStat.MoveSpeedBonus)) / 2;

        ch.AddStat(CharacterStat.AddDex, -subDex);
        ch.AddStat(CharacterStat.AddAgi, -subAgi);
        ch.AddStat(CharacterStat.AspdBonus, -20);
        ch.AddStat(CharacterStat.MoveSpeedBonus, -subSpeed);

        state.Value1 += subDex;
        state.Value2 += subAgi;
        state.Value3 += (short)subSpeed;
        state.Value4 += 1;

        ch.ModifyExistingCastTime(1.5f);

        ch.UpdateStats(); //this isn't automatic if you change things in OnUpdate

        return StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        state.Value1 = ch.GetEffectiveStat(CharacterStat.Dex) / 2;
        state.Value2 = ch.GetEffectiveStat(CharacterStat.Agi) / 2;
        state.Value3 = (short)((100 + ch.GetEffectiveStat(CharacterStat.MoveSpeedBonus)) / 2);
        state.Value4 = 1;

        ch.AddStat(CharacterStat.AddDex, -state.Value1);
        ch.AddStat(CharacterStat.AddAgi, -state.Value2);
        ch.AddStat(CharacterStat.AspdBonus, -20);
        ch.AddStat(CharacterStat.MoveSpeedBonus, -state.Value3);
        ch.SetBodyState(BodyStateFlags.Pacification);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AddDex, -state.Value1);
        ch.SubStat(CharacterStat.AddAgi, -state.Value2);
        ch.SubStat(CharacterStat.AspdBonus, -20 * state.Value4);
        ch.SubStat(CharacterStat.MoveSpeedBonus, -state.Value3);
        ch.RemoveBodyState(BodyStateFlags.Pacification);
    }
}