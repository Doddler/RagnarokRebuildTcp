using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.FireBall, SkillClass.Magic)]
public class FireBallHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => lvl <= 5 ? 1.5f : 1f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null || !target.IsTargetable)
            return;

        //lvl = int.Clamp(lvl, 1, 10);
        var map = source.Character.Map!;
        
        map.AddVisiblePlayersAsPacketRecipients(source.Character, target.Character); //combines src and target visibility lists

        var skillModifier = 1.4f + 0.2f * lvl;
        var blastRadius = 2;
        if (source.Character.Type == CharacterType.Monster && lvl > 10)
            blastRadius = 3;
        var attack = new AttackRequest(CharacterSkill.FireBall, skillModifier, 1, AttackFlags.Magical, AttackElement.Fire);

        //initial hit against the targeted enemy
        var res = source.CalculateCombatResult(target, attack);
        source.ApplyAfterCastDelay(1f);
        res.AttackMotionTime = 0.38f;
        res.IsIndirect = isIndirect;
        var baseTime = Time.ElapsedTimeFloat + res.AttackMotionTime +
                       source.Character.Position.DistanceTo(target.Character.Position) / ServerConfig.ArrowTravelTime;
        res.Time = baseTime;
        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.FireBall, lvl, res);

        source.ExecuteCombatResult(res, false);

        //splash damage to targets nearby
        using var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, target.Character.Position, blastRadius, targetList, true, true);

        foreach (var e in targetList)
        {
            if (e == target.Entity)
                continue;

            if (!e.TryGet<WorldObject>(out var blastTarget))
                continue;

            var distanceFromBlast = target.Character.WorldPosition.DistanceTo(blastTarget.WorldPosition);
            attack.AttackMultiplier = skillModifier * (1f - distanceFromBlast * 0.15f); //15% falloff per tile distance
            res = source.CalculateCombatResult(e.Get<CombatEntity>(), attack);
            res.AttackMotionTime = 0;
            res.Time = baseTime + distanceFromBlast * 0.03f;

            source.ExecuteCombatResult(res, false);
            CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
        }

        if(!isIndirect)
            source.ApplyCooldownForAttackAction(position);
        CommandBuilder.ClearRecipients();
    }
}