using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System;
using System.Threading.Channels;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[SkillHandler(CharacterSkill.BlitzBeat, SkillClass.Physical, SkillTarget.Enemy)]
public class BlitzBeatHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1.5f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null || !target.IsTargetable || source.Character.Type != CharacterType.Player)
            return;

        var map = source.Character.Map!;

        var baseDamage = 80 + source.GetEffectiveStat(CharacterStat.Dex) / 5 + source.GetEffectiveStat(CharacterStat.Int)
                         + 6 * source.Player.MaxLearnedLevelOfSkill(CharacterSkill.SteelCrow);

        //custom change: add crit damage effects apply to blitz beat
        baseDamage += baseDamage * (source.GetStat(CharacterStat.AddCritDamage) + target.GetStat(CharacterStat.AddCritDamageRaceFormless + (int)target.GetRace())) / 100;

        var hitCount = lvl;
        if (isIndirect)
            hitCount = 1 + (source.Player.GetData(PlayerStat.JobLevel) - 1) / 10;
        hitCount = int.Clamp(hitCount, 1, 5);

        var properDamage = baseDamage; // * multiplier;
        var flags = AttackFlags.IgnoreDefense | AttackFlags.IgnoreEvasion | AttackFlags.Physical | AttackFlags.Ranged | AttackFlags.NoDamageModifiers
                    | AttackFlags.IgnoreWeaponRefine | AttackFlags.NoTriggerOnAttackEffects | AttackFlags.NoTriggerWhenAttackedEffects;

        using var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, target.Character.Position, 1, targetList, true, true);
        //targetList.Shuffle();

        var req = new AttackRequest(CharacterSkill.BlitzBeat, 1, hitCount, flags, AttackElement.Neutral);
        if (isIndirect)
            req.MinAtk = req.MaxAtk = properDamage * 2 / (1 + targetList.Count);
        else
            req.MinAtk = req.MaxAtk = properDamage;

        var res = source.CalculateCombatResult(target, req);
        res.TimeInSeconds = 0.2f;
        res.IsIndirect = isIndirect;
        source.ExecuteCombatResult(res);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.BlitzBeat, lvl, res);

        foreach (var e in targetList)
        {
            if (e == target.Entity)
                continue;

            if (!e.TryGet<WorldObject>(out var blastTarget))
                continue;

            res = source.CalculateCombatResult(e.Get<CombatEntity>(), req);
            res.TimeInSeconds = 0.2f;

            map.AddVisiblePlayersAsPacketRecipients(blastTarget);
            source.ExecuteCombatResult(res, false);
            CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
            CommandBuilder.ClearRecipients();
        }

        if (!isIndirect)
        {
            //source.ApplyCooldownForSupportSkillAction();
            source.ApplyAfterCastDelay(1f);
        }

        CommandBuilder.ClearRecipients();
    }
}