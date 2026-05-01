using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._2ndJobs;

[StatusEffectHandler(CharacterStatusEffect.Quagmire, StatusClientVisibility.Owner, StatusEffectFlags.NoSave)]
public class StatusQuagmire : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnMove;

    public override StatusUpdateResult OnMove(CombatEntity ch, ref StatusEffectState state, Position src, Position dest, bool isTeleport)
    {
        if (ch.Character.Type != CharacterType.Player)
            return StatusUpdateResult.Continue;

        var map = ch.Character.Map;
        if (map != null && map.TryGetAreaOfEffectAtPosition(dest, CharacterSkill.Quagmire, out var effect))
        {
            state.Expiration = effect.Expiration; //we might have moved into a new quagmire, so update
            return StatusUpdateResult.Continue;
        }

        return StatusUpdateResult.EndStatus;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.AddStat(CharacterStat.AddAgi, -state.Value1);
        ch.AddStat(CharacterStat.AddDex, -state.Value2);
        ch.AddStat(CharacterStat.MoveSpeedBonus, -40);

        ServerLogger.Debug($"Quagmire on target, new stats agi {ch.GetEffectiveStat(CharacterStat.Agi)} dex {ch.GetEffectiveStat(CharacterStat.Dex)}");
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AddAgi, -state.Value1);
        ch.SubStat(CharacterStat.AddDex, -state.Value2);
        ch.SubStat(CharacterStat.MoveSpeedBonus, -40);

        ServerLogger.Debug($"Quagmire lost, new stats agi {ch.GetEffectiveStat(CharacterStat.Agi)} dex {ch.GetEffectiveStat(CharacterStat.Dex)}");
    }
}
