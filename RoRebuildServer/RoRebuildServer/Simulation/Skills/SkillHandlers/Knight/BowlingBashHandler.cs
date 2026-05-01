using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Knight;

[SkillHandler(CharacterSkill.BowlingBash, SkillClass.Physical)]
public class BowlingBashHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0.7f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null || !target.IsTargetable || source.Character.Map == null)
            return;

        var map = source.Character.Map;
        Debug.Assert(map != null);

        using var potentialTargets = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, target.Character.Position, 5, potentialTargets, true, true);

        potentialTargets.Remove(ref target.Entity); //we don't want the skill to chain to our first target, so remove them from the list

        source.ApplyCooldownForAttackAction(target.Character.Position);

        var srcPos = source.Character.Position;
        var targetPos = target.Character.Position;
        var dir = source.Character.FacingDirection;
        if (srcPos != targetPos)
            dir = (targetPos - srcPos).Normalize().GetDirectionForOffset();

        BowlingBashAttack(map.WalkData, source, target, potentialTargets, dir, lvl, 0);
    }

    private void BowlingBashAttack(MapWalkData walk, CombatEntity src, CombatEntity target, EntityList potentialTargets,
        Direction knockbackDirection, int skillLevel, int recursionStep)
    {
        //var delay = recursionStep > 0 ? 0f : -0.1f;

        var res = HitTarget(src, target, skillLevel, false, recursionStep == 0); //first hit check.
        if (!res)
            return; //did our hit miss? Shame, no chaining.

        HitTarget(src, target, skillLevel, true, false); //second hit happens 0.1s later.

        var pos = target.Character.Position.AddDirectionToPosition(knockbackDirection);
        if (!walk.IsCellWalkable(pos))
            return; //if the target is not in a position where it can be knocked back (ie: it's being pushed into a wall), the chain fails.

        //a funny quirk is that a boss monster won't be knocked back, but the collision check still occurs as if it moved.
        if (pos != target.Character.Position && target.GetSpecialType() != CharacterSpecialType.Boss)
            target.Character.Map!.ChangeEntityPosition3(target.Character, target.Character.WorldPosition, pos, false);

        for (var t = 0; t < potentialTargets.Count; t++)
        {
            var hit = potentialTargets[t].Get<CombatEntity>();
            if (pos.SquareDistance(hit.Character.Position) > 1)
                continue;

            potentialTargets.SwapFromBack(t);
            t--;

            var knockDir = (Direction)GameRandom.Next(8); //knockback for chained targets is always in a random direction

            //max recursion level is 9 with level 10 bowling bash
            if (recursionStep < MathF.Max(1, skillLevel - 1))
                BowlingBashAttack(walk, src, hit, potentialTargets, knockDir, skillLevel, recursionStep + 1);
        }
    }

    private bool HitTarget(CombatEntity src, CombatEntity target, int skillLevel, bool hasDelay, bool isSkillHit)
    {
        var attack = new AttackRequest(CharacterSkill.BowlingBash, 1 + 0.4f * skillLevel, 1, AttackFlags.Physical, AttackElement.None);
        var res = src.CalculateCombatResult(target, attack);
        if (!hasDelay)
            res.Time -= 0.2f;
        //res.Time += timeDelay;

        if (isSkillHit)
        {
            src.Character.Map?.AddVisiblePlayersAsPacketRecipients(src.Character, target.Character);
            CommandBuilder.SkillExecuteTargetedSkill(src.Character, target.Character, CharacterSkill.BowlingBash, skillLevel, res);
            CommandBuilder.ClearRecipients();
        }
        else
        {
            res.AttackSkill = CharacterSkill.None;
            CommandBuilder.AttackAutoVis(src.Character, target.Character, res, false);
        }

        src.ExecuteCombatResult(res, false);

        return res.IsDamageResult;
    }
}