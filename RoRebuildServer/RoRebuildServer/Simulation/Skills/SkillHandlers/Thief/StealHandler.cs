using System.Diagnostics;
using System.Threading;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief;

[SkillHandler(CharacterSkill.Steal, SkillClass.Unique, SkillTarget.Enemy)]
public class StealHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect)
    {
        if (target == null)
            return SkillValidationResult.InvalidTarget;

        if (target.GetSpecialType() == CharacterSpecialType.Boss)
            return SkillValidationResult.CannotTargetBossMonster;

        if (source.Character.Type != CharacterType.Player || target.Character.Type != CharacterType.Monster)
            return SkillValidationResult.InvalidTarget;

        if (target.HasStatusEffectOfType(CharacterStatusEffect.StolenFrom))
            return SkillValidationResult.ItemAlreadyStolen;

        return base.ValidateTarget(source, target, position, lvl, false);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        Debug.Assert(target != null);
        Debug.Assert(target.Character.Type == CharacterType.Monster);

        var srcDex = source.GetEffectiveStat(CharacterStat.Dex);
        var targetDex = target.GetEffectiveStat(CharacterStat.Dex);

        var basicRate = 4 + lvl * 6 + (srcDex - targetDex) / 2;
        var rate = basicRate / 100f;

        if (!DataManager.MonsterDropData.TryGetValue(target.Character.Monster.MonsterBase.Code, out var drops))
        {
            CommandBuilder.SkillFailed(source.Player, SkillValidationResult.Failure);
            return;
        }

        foreach (var d in drops.DropChances)
        {
            var chance = (int)(d.Chance * 10 * rate);
            if (GameRandom.Next(0, 100_000) > chance)
                continue;

            var count = d.CountMax == d.CountMin ? d.CountMin : GameRandom.NextInclusive(d.CountMin, d.CountMax);
            var item = new ItemReference(d.Id, count);
            source.Player.CreateItemInInventory(item);

            var di = DamageInfo.EmptyResult(source.Entity, target.Entity);
            CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Steal, lvl, di);
            
            target.AddStatusEffect(CharacterStatusEffect.StolenFrom, int.MaxValue);

            return;
        }

        CommandBuilder.SkillFailed(source.Player, SkillValidationResult.Failure);
    }
}