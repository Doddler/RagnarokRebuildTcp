using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.Misc;

[StatusEffectHandler(CharacterStatusEffect.DungeonMiasma, StatusClientVisibility.Owner, StatusEffectFlags.StayOnClear)]
public class DungeonMiasmaStatus : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnMove;

    public override StatusUpdateResult OnChangeMaps(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Map == null || ch.Character.Map.MapWideStatusEffect != CharacterStatusEffect.DungeonMiasma)
            return StatusUpdateResult.EndStatus;
        return StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type == CharacterType.Player)
        {

        }

        if (ch.Character.Type == CharacterType.Monster)
        {
            ch.AddStat(CharacterStat.AddAttackPercent, 30);
            ch.AddStat(CharacterStat.AddAgi, ch.GetStat(CharacterStat.Level) / 5);
            ch.AddStat(CharacterStat.AddDex, ch.GetStat(CharacterStat.Level) / 5);
            ch.AddStat(CharacterStat.AddDefPercent, 15);
            ch.AddStat(CharacterStat.AddMDefPercent, 15);
            ch.AddStat(CharacterStat.AddMaxHpPercent, 30);
            ch.AddStat(CharacterStat.AddDropPercent, 100);
            ch.AddStat(CharacterStat.AddExpPercent, 60);

            ch.Character.Monster.RefreshMaxHp(ch.GetSpecialType() == CharacterSpecialType.Boss);
        }
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type == CharacterType.Player)
        {

        }

        if (ch.Character.Type == CharacterType.Monster)
        {
            ch.SubStat(CharacterStat.AddAttackPercent, 30);
            ch.SubStat(CharacterStat.AddAgi, ch.GetStat(CharacterStat.Level) / 5);
            ch.SubStat(CharacterStat.AddDex, ch.GetStat(CharacterStat.Level) / 5);
            ch.SubStat(CharacterStat.AddDefPercent, 15);
            ch.SubStat(CharacterStat.AddMDefPercent, 15);
            ch.SubStat(CharacterStat.AddMaxHpPercent, 30);
            ch.SubStat(CharacterStat.AddDropPercent, 100);
            ch.SubStat(CharacterStat.AddExpPercent, 60);

            ch.Character.Monster.RefreshMaxHp(ch.GetSpecialType() == CharacterSpecialType.Boss);
        }
    }
}
