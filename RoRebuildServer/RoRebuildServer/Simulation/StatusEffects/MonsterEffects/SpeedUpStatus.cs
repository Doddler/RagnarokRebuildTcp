using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.SpeedUp, StatusClientVisibility.None, StatusEffectFlags.None, "SpeedBonus")]
public class SpeedUpStatus : StatusEffectBase
{
    private const int MoveSpeedBonus = 100;

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        state.Value2 = ch.GetEffectiveStat(CharacterStat.Agi) * 20 / 100;
        ch.AddStat(CharacterStat.AddFlee, state.Value2);
        ch.AddStat(CharacterStat.MoveSpeedBonus, MoveSpeedBonus);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SubStat(CharacterStat.AddFlee, state.Value2);
        ch.SubStat(CharacterStat.MoveSpeedBonus, MoveSpeedBonus);
    }
}