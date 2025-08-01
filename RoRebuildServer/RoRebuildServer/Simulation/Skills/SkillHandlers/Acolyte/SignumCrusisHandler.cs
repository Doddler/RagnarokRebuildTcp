using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[SkillHandler(CharacterSkill.SignumCrusis, SkillClass.Magic, SkillTarget.Self)]
public class SignumCrusisHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        Debug.Assert(source.Character.Map != null);
        source.ApplyCooldownForSupportSkillAction();
        source.ApplyAfterCastDelay(2f);

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.SignumCrusis, 86400, 10 + lvl * 4);

        using var entities = EntityListPool.Get();
        source.Character.Map.GatherEnemiesInRange(source.Character, 15, entities, false, true);
        foreach (var e in entities)
        {
            if (e.Type == EntityType.Player || !e.TryGet<CombatEntity>(out var enemy))
                continue;

            if (!enemy.IsValidTarget(source, false, true))
                continue;

            if (enemy.GetRace() != CharacterRace.Demon && !enemy.IsElementBaseType(CharacterElement.Undead1))
                continue;

            if (enemy.HasStatusEffectOfType(CharacterStatusEffect.SignumCrusis))
                continue;

            if (GameRandom.Next(0, 100) > 25 + lvl * 4 + source.GetStat(CharacterStat.Level) - enemy.GetStat(CharacterStat.Level))
                continue;

            enemy.AddStatusEffect(status);
            enemy.Character.Map?.AddVisiblePlayersAsPacketRecipients(source.Character);
            CommandBuilder.SendEmoteMulti(enemy.Character, 4); //sweat drop
            CommandBuilder.ClearRecipients();
        }

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.SignumCrusis, lvl, isIndirect);
    }
}