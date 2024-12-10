using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;
using RoRebuildServer.EntityComponents.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.NapalmBeat, SkillClass.Magic, SkillTarget.Enemy)]
public class NapalmBeatHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        return lvl switch
        {
            <= 3 => 1f,
            <= 5 => 0.9f,
            <= 7 => 0.8f,
            <= 8 => 0.7f,
            <= 9 => 0.6f,
            10 => 0.5f,
            _ => 1f
        };
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null || !target.IsValidTarget(source))
            return;

        var map = source.Character.Map;
        Debug.Assert(map != null);

        lvl = int.Clamp(lvl, 1, 10);

        //gather all players who can see either the caster or target as recipients of the following packets
        map.AddVisiblePlayersAsPacketRecipients(source.Character, target.Character);

        //first, hit the target with Napalm Beat fair and square
        using var targetList = EntityListPool.Get();
        var req = new AttackRequest(CharacterSkill.NapalmBeat, 1f + 0.1f * lvl, 1, AttackFlags.Physical | AttackFlags.IgnoreEvasion, AttackElement.Ghost);
        (req.MinAtk, req.MaxAtk) = source.CalculateAttackPowerRange(true);
        var res = source.CalculateCombatResultUsingSetAttackPower(target, req);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false); //apply damage to target

        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.NapalmBeat, lvl, res); //send cast packet

        //now gather all players getting hit
        map.GatherEnemiesInArea(source.Character, source.Character.Position, 1, targetList, !isIndirect, true);

        //deal damage to all enemies 2/3 the damage dealt to the primary target
        res.Damage = res.Damage * 2 / 3;
        if (res.Damage > 0)
        {
            foreach (var e in targetList)
            {
                if (e == target.Entity)
                    continue; //we've already hit them

                if (!e.TryGet<WorldObject>(out var blastTarget))
                    continue;

                res.Target = e;
                blastTarget.CombatEntity.ExecuteCombatResult(res, false);  //apply damage to target
                CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
            }
        }

        CommandBuilder.ClearRecipients();
    }
}

