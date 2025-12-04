using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.NapalmBeat, SkillClass.Magic, SkillTarget.Enemy)]
public class NapalmBeatHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        return 1.2f - lvl * 0.1f;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null || !target.IsValidTarget(source))
            return;

        var map = source.Character.Map;
        Debug.Assert(map != null);

        lvl = int.Clamp(lvl, 1, 10);

        //gather all players who can see either the caster or target as recipients of the following packets
        map.AddVisiblePlayersAsPacketRecipients(source.Character, target.Character);

        var flags = AttackFlags.Physical | AttackFlags.IgnoreEvasion;
        var mult = 1f + 0.15f * lvl;
        var range = 1;
        if ((target.BodyState & (BodyStateFlags.Frozen | BodyStateFlags.Petrified)) > 0)
        {
            mult *= 2;
            range = 3;
        }

        //first, hit the target with Napalm Beat fair and square
        using var targetList = EntityListPool.Get();
        var req = new AttackRequest(CharacterSkill.NapalmBeat, mult, 1, flags, AttackElement.Ghost);
        (req.MinAtk, req.MaxAtk) = source.CalculateAttackPowerRange(true);
        var res = source.CalculateCombatResultUsingSetAttackPower(target, req);
        source.ApplyAfterCastDelay(1f - lvl * 0.05f, ref res);
        res.Time = Time.ElapsedTimeFloat + res.AttackMotionTime;
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false); //apply damage to target

        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.NapalmBeat, lvl, res, isIndirect); //send cast packet

        //now gather all players getting hit
        map.GatherEnemiesInArea(source.Character, target.Character.Position, range, targetList, !isIndirect, true);

        //deal damage to all enemies 2/3 the damage dealt to the primary target
        res.Damage = res.Damage * 2 / 3;
        if (res.Damage > 0)
        {
            foreach (var e in targetList)
            {
                if (e == target.Entity)
                    continue; //we've already hit them

                if (!e.TryGet<CombatEntity>(out var blastTarget))
                    continue;

                if (!blastTarget.IsValidTarget(source))
                    continue;

                res.Target = e;
                blastTarget.ExecuteCombatResult(res, false); //apply damage to target
                CommandBuilder.AttackMulti(source.Character, blastTarget.Character, res, false);
            }
        }

        CommandBuilder.ClearRecipients();
    }
}