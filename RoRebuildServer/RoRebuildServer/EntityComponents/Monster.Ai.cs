using System;
using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Items;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

public partial class Monster
{
    #region InputStateChecks

    /// <summary>
    /// Executes an input state check for a specific state transition.
    /// Input state transition checks should not modify a monster's behavior, while an output state transition does.
    /// </summary>
    /// <returns>True if the state should change, false if the state transition should fail.</returns>
    /// TODO: Some input state checks change the monster's target, where as that shouldn't happen until the output state checks.
    private bool InputStateCheck(MonsterInputCheck inCheckType)
    {
        switch (inCheckType)
        {
            case MonsterInputCheck.InWaitEnd: return InWaitEnd();
            case MonsterInputCheck.InChangeNormal: return InChangeNormal();
            case MonsterInputCheck.InEnemyOutOfSight: return InEnemyOutOfSight();
            case MonsterInputCheck.InEnemyOutOfAttackRange: return InEnemyOutOfAttackRange();
            case MonsterInputCheck.InReachedTarget: return InReachedTarget();
            case MonsterInputCheck.InReachedRandomMoveTarget: return InReachedRandomMoveTarget();
            case MonsterInputCheck.InTargetSearch: return InTargetSearch();
            case MonsterInputCheck.InAttackRange: return InAttackRange();
            case MonsterInputCheck.InAttackRangeAny: return InAttackRangeAny();
            case MonsterInputCheck.InNeedAttackingAdjust: return InNeedAttackingAdjust();
            case MonsterInputCheck.InAttackDelayEnd: return InAttackDelayEnd();
            case MonsterInputCheck.InAttacked: return InAttacked();
            case MonsterInputCheck.InAttackedNoSwap: return InAttacked(false);
            case MonsterInputCheck.InMeleeAttacked: return InMeleeAttacked();
            case MonsterInputCheck.InDeadTimeoutEnd: return InDeadTimeoutEnd();
            case MonsterInputCheck.InAllyInCombat: return InAllyInCombat();
            case MonsterInputCheck.InOwnerOutOfSight: return InOwnerOutOfSight();
            case MonsterInputCheck.InOwnerAttacked: return InOwnerAttacked();
            case MonsterInputCheck.InTargetedForSkill: return InTargetLocked();
            case MonsterInputCheck.InItemInSight: return InItemInSight();
            case MonsterInputCheck.InItemGone: return InItemGone();
            case MonsterInputCheck.InReachedItem: return InReachedItem();
            case MonsterInputCheck.InNoCondition: return true;
        }

        return false;
    }

    /// <summary> Checks if an elapsed wait time has passed before a monster is allowed to random move again. </summary>
    private bool InWaitEnd()
    {
        if (nextMoveUpdate <= Time.ElapsedTimeFloat || MonsterBase.MoveSpeed < 0)
            return true;

        return false;
    }

    /// <summary> Checks if the monster has reached it's destination yet. </summary>
    private bool InReachedTarget()
    {
        return Character.State != CharacterState.Moving || Character.Position == Character.TargetPosition;
    }

    private bool InChangeNormal()
    {
        return CombatEntity.GetStat(CharacterStat.Disabled) <= 0;
    }

    private bool AdjustToAdjacentTile()
    {
        var start = GameRandom.NextInclusive(1) == 1 ? -1 : 1;
        var dir = -start;

        for (var x = start; x >= -1 && x <= 1; x += dir)
        {
            for (var y = start; y >= -1 && y <= 1; y += dir)
            {
                if (y == 0 && x == 0)
                    continue;

                var pos = Character.Position + new Position(x, y);

                if (!Character.Map!.IsTileOccupied(pos))
                {
                    if (Character.TryMove(pos, 0))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary> Checks if the monster has reached it's destination yet, but also adjust the target one time if stacked. </summary>
    private bool InReachedRandomMoveTarget()
    {
        if (Character.State != CharacterState.Moving)
        {
            if (Character.Map!.IsEntityStacked(Character))
            {
                if (AdjustToAdjacentTile())
                {
                    //inAdjustMove = true;
                    return false; //we want to stay in random move while we adjust to a non stacked position
                }
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks the status of the monster's current target, and returns true if the monster can no longer see or reach that target.
    /// </summary>
    private bool InEnemyOutOfSight()
    {
        if (!ValidateTarget() || targetCharacter == null)
            return true;

        if (!targetCharacter.CombatEntity.IsValidTarget(CombatEntity))
            return true;

        if (targetCharacter.Position == Character.Position)
            return false;

        var outOfAttackRange = InEnemyOutOfAttackRange();

        if (CombatEntity.BodyState.HasFlag(BodyStateFlags.Blind) && outOfAttackRange)
            return true;

        if (targetCharacter.Position.SquareDistance(Character.Position) > MonsterBase.ChaseDist + 2)
            return true;

        if (Character.MoveSpeed < 0 && outOfAttackRange)
            return true;

        if (Character.Map != null && !Character.Map.WalkData.HasLineOfSight(Character.Position, targetCharacter.Position))
        {
            if (!Character.Map.Instance.Pathfinder.HasPath(Character.Map.WalkData, Character.Position, targetCharacter.Position, 1))
                return true;
        }

        return false;
    }

    /// <summary> Checks if the monster is within range to attack it's current target or not. </summary>
    private bool InAttackRange()
    {
        if (Character.Map != null && Character.Map.PlayerCount == 0)
            return false;

        //do we have a target? If we do and it's not valid, remove it.
        if (!ValidateTarget())
            Target = Entity.Null;

        var target = targetCharacter;
        //var checkPosition = Character.Position;
        //if (Character.IsMoving && Character.WalkPath != null)
        //    checkPosition = Character.WalkPath[Character.MoveStep + 1];

        //if we have a character still, check if we're in range.
        if (Target == Entity.Null || target == null) return false;

        if (target.Position.DistanceTo(Character.Position) <= MonsterBase.Range)
            return Character.Map!.WalkData.HasLineOfSight(Character.Position, target.Position);

        //failed. Return false.
        return false;
    }


    /// <summary> We don't care who we're next to, if it isn't our target we'll pick a new one. </summary>
    private bool InAttackRangeAny()
    {
        if (Character.Map != null && Character.Map.PlayerCount == 0)
            return false;

        if (InAttackRange())
            return true;

        //if we don't have a target, check if we can acquire one quickly
        if (!FindRandomTargetInRange(MonsterBase.Range, out var newTarget))
            return false;

        //var checkPosition = Character.Position;
        //if (Character.IsMoving && Character.WalkPath != null)
        //    checkPosition = Character.WalkPath[Character.MoveStep + 1];

        //we have a character. Check if it's in range, and if so, assign it as our current target
        var target = newTarget.Get<WorldObject>();
        if (target.Position.DistanceTo(Character.Position) <= MonsterBase.Range)
        {
            SwapTarget(newTarget);
            //Target = newTarget;
            return true;
        }

        //failed. Return false.
        return false;
    }

    /// <summary>
    /// Checks if the monster is currently stacked on top of another monster.
    /// There is a random element here, the monster only has a 50% chance to care when checked.
    /// </summary>
    private bool InNeedAttackingAdjust()
    {
        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
            return false;

        if (GameRandom.NextInclusive(0, 1) < 1)
            return false;

        if (Character.Map!.IsEntityStacked(Character))
            return true;

        return false;
    }

    /// <summary> Has the monster's attack cooldown expired yet? </summary>
    private bool InAttackDelayEnd()
    {
        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
            return false;

        return true;
    }

    /// <summary> Scans for any allies of the same type within sight that are in combat, and if so, assist them. </summary>
    /// TODO: We should not commit to switching target until the output state check occurs.
    private bool InAllyInCombat()
    {
        if (!Character.Map!.QuickCheckPlayersNearby(Character, 15))
            return false;

        if (CanAssistAlly(10, out var target))
        {
            SwapTarget(target);
            return true;
        }

        return false;
    }

    /// <summary> Checks if the monster has been attacked by any player since the last AI update. </summary>
    /// <param name="swapToNewAttacker">Should we cause the monster to change his target in response to being attacked?</param>
    /// TODO: We should not commit to switching target until the output state check occurs.
    private bool InAttacked(bool swapToNewAttacker = true)
    {
        if (!WasAttacked)
            return false;

        if (!Character.LastAttacked.TryGet<CombatEntity>(out var ce) || !ce.IsValidTarget(CombatEntity))
            return false;

        if (swapToNewAttacker && Target != Character.LastAttacked)
            SwapTarget(Character.LastAttacked);

        return true;
    }

    //similar to InAttacked, but only returns true if the attacker is close enough for us to attack back
    private bool InMeleeAttacked()
    {
        if (!WasAttacked)
            return false;

        if (!Character.LastAttacked.TryGet<CombatEntity>(out var ce) || !ce.IsValidTarget(CombatEntity))
            return false;

        var target = ce.Character;

        if (target.Position.DistanceTo(Character.Position) <= MonsterBase.Range)
            return Character.Map!.WalkData.HasLineOfSight(Character.Position, target.Position);

        return false;
    }

    // did a player lock onto us with a spell?
    private bool InTargetLocked()
    {
        if (!WasMagicLocked)
            return false;

        if (!Character.LastAttacked.TryGet<CombatEntity>(out var ce) || !ce.IsValidTarget(CombatEntity))
            return false;

        if (Target != Character.LastAttacked)
            SwapTarget(Character.LastAttacked);

        return true;
    }

    /// <summary> Checks if the current target has moved beyond attack range. </summary>
    private bool InEnemyOutOfAttackRange()
    {
        if (!ValidateTarget())
            return false;

        var target = targetCharacter;
        if (target == null)
            return false;

        if (target.Position.DistanceTo(Character.Position) > MonsterBase.Range)
            return true;

        Debug.Assert(Character.Map != null, "Character.Map != null");
        if (!Character.Map.WalkData.HasLineOfSight(Character.Position, target.Position))
            return true;

        return false;
    }

    /// <summary>
    /// Looks to see if there are any valid targets within scan distance. Switches target to the new target.
    /// </summary>
    /// TODO: We should not commit to switching target until the output state check occurs.
    private bool InTargetSearch()
    {
        if (Character.Map == null || Character.Map.PlayerCount == 0)
            return false;

        if (CombatEntity.BodyState.HasFlag(BodyStateFlags.Blind))
            return false;

        if (!FindRandomTargetInRange(MonsterBase.ScanDist, out var newTarget))
            return false;

        SwapTarget(newTarget);
        return true;
    }

    /// <summary>
    /// Checks if the monster's respawn timer has expired, and if the monster should attempt to respawn.
    /// </summary>
    private bool InDeadTimeoutEnd()
    {
        if (deadTimeout > Time.ElapsedTimeFloat)
        {
            nextAiUpdate = deadTimeout + 0.01f;
            return false;
        }

        return true;
    }

    private bool InOwnerOutOfSight()
    {
        if (!Master.IsAlive())
            return false;

        var m = Master.Get<Monster>();
        if (!Character.Position.InRange(m.Character.Position, 2))
        {
            inAdjustMove = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Responds to the owner being in combat, and if so assumes that target itself.
    /// </summary>
    /// <returns></returns>
    private bool InOwnerAttacked()
    {
        if (!Master.IsAlive())
            return false;

        var m = Master.Get<Monster>();
        if (m.Target.IsAlive())
        {
            var target = m.Target.Get<CombatEntity>();

            if (target.IsValidTarget(CombatEntity))
            {
                SwapTarget(m.Target);
                return true;
            }
        }

        return false;
    }

    private bool InItemInSight()
    {
        if (IsInventoryFull)
            return false;

        if (Character.Map == null || Character.Map.ItemChunkLookup.Count == 0)
            return false;

        GroundItem item = default;
        if (!Character.Map.FindRandomGroundItemInRange(Character.Position, 9, ref item))
            return false;

        Character.TargetPosition = item.Position;
        Character.ItemTarget = item.Id;

        return true;
    }

    private bool InItemGone()
    {
        if (Character.Map == null)
            return false;

        if (Character.Map.ItemChunkLookup.ContainsKey(Character.ItemTarget))
            return false;

        return true;
    }

    private bool InReachedItem()
    {
        return Character.Position == Character.TargetPosition;
    }

    #endregion

    #region OutputStateChecks

    /// <summary>
    /// Executes an output state check for a specific state transition.
    /// </summary>
    /// <param name="outCheckType"></param>
    /// <returns>True if the state should change, false if the state transition should fail.</returns>
    private bool OutputStateCheck(MonsterOutputCheck outCheckType)
    {
        switch (outCheckType)
        {
            case MonsterOutputCheck.OutRandomMoveStart: return OutRandomMoveStart();
            case MonsterOutputCheck.OutWaitStart: return OutWaitStart();
            case MonsterOutputCheck.OutSearch: return OutSearch();
            case MonsterOutputCheck.OutStartChase: return OutStartChase();
            case MonsterOutputCheck.OutChangeTargets: return OutChangeTargets();
            case MonsterOutputCheck.OutTryAttacking: return OutTryAttacking();
            case MonsterOutputCheck.OutStartAttacking: return OutStartAttacking();
            case MonsterOutputCheck.OutPerformAttack: return OutPerformAttack();
            case MonsterOutputCheck.OutAttackingAdjust: return OutAttackingAdjust();
            case MonsterOutputCheck.OutTryRevival: return OutTryRevival();
            case MonsterOutputCheck.OutMoveToOwner: return OutMoveToOwner();
            case MonsterOutputCheck.OutMoveToItem: return OutMoveToItem();
            case MonsterOutputCheck.OutPickUpItem: return OutPickUpItem();
            case MonsterOutputCheck.OutStopMoving: return OutStopMoving();
            case MonsterOutputCheck.OutDebug: return OutDebug();
        }

        return false;
    }

    private bool OutDebug()
    {
        if (CombatEntity.IsCasting || Character.InAttackCooldown || !FindRandomTargetInRange(9, out var newTarget))
            return false;

        nextMoveUpdate = Time.ElapsedTimeFloat + GameRandom.NextFloat(4f, 6f);

        return true;
    }

    /// <summary>
    /// Waits a random amount of time before the next Random Move should be allowed to execute.
    /// If the monster is stacked with another monster, it will reset the wait time.
    /// </summary>
    private bool OutWaitStart()
    {
        Target = Entity.Null;

        var extraTime = 0f;

        if (Character.Map!.IsEntityStacked(Character))
            if (AdjustToAdjacentTile())
                extraTime = 2f;
        
        nextMoveUpdate = Time.ElapsedTimeFloat + GameRandom.NextFloat(4f, 6f) + extraTime;
        inAdjustMove = false;

        return true;
    }

    /// <summary>
    /// State transition where the monster will be unable to move for a very long period of time.
    /// </summary>
    private bool OutWaitForever()
    {
        nextAiUpdate += 100_000_000f;
        return true;
    }

    /// <summary>
    /// Attempts to find a random target that the monster can reach within a specified distance.
    /// </summary>
    /// <param name="area">The bounds to search for a walkable tile.</param>
    /// <param name="targetPos">The monster's starting location.</param>
    /// <param name="distance">The maximum distance the monster can path.</param>
    /// <param name="attempts">How many locations we should test before giving up.</param>
    private bool FindRandomMoveTargetInArea(Area area, Position targetPos, int distance, int attempts)
    {
        var moveArea = Area.CreateAroundPoint(targetPos, distance).ClipArea(area);
        var newPos = Position.RandomPosition(moveArea);

        for (var i = 0; i < attempts; i++)
        {
            //spend the first 25% of checks skipping any moves only 2 tiles away
            if (i < attempts / 4f && (targetPos.SquareDistance(newPos) <= 2 || Character.Map!.IsTileOccupied(newPos)))
                continue;


            if (newPos != Character.Position && Character.TryMove(newPos, 0))
                return true;

            newPos = Position.RandomPosition(moveArea);
        }

        return false;
    }

    /// <summary>
    /// Attempts to start moving to a random position within range.
    /// If a monster is locked to a specific region, it will attempt to find a target within that space first.
    /// </summary>
    private bool OutRandomMoveStart()
    {
        if (MonsterBase.MoveSpeed < 0)
            return false;

        if (LockMovementToSpawn && SpawnRule != null)
        {
            if (FindRandomMoveTargetInArea(SpawnRule.SpawnArea, Character.Position, 9, 10))
                return true;
        }

        return FindRandomMoveTargetInArea(Character.Map!.MapBounds, Character.Position, 9, 20);
    }

    /// <summary>
    /// Attempts to cause the monster to move to an unoccupied tile that remains within attack range of it's target.
    /// It will most likely attempt to move only 1 tile, but it can rarely chose to move 2 or 3 tiles.
    /// Only makes one attempt per check to adjust.
    /// </summary>
    private bool OutAttackingAdjust()
    {
        if (MonsterBase.MoveSpeed < 0)
            return false;

        Debug.Assert(targetCharacter != null, $"Monster {Character.Name} attempting to perform attack adjustment while targetCharacter is null.");

        var rnd = GameRandom.NextInclusive(0, 100); //very rarely, scan further than normal
        var range = rnd switch
        {
            < 80 => 1,
            < 95 => 2,
            _ => 3
        };

        var area = Area.CreateAroundPoint(Character.Position, range);
        area.ClipArea(Area.CreateAroundPoint(targetCharacter.Position, CombatEntity.GetStat(CharacterStat.Range)));

        var newPos = Position.RandomPosition(area);

        if (!Character.Map!.WalkData.IsCellWalkable(newPos))
            return false;

        if (newPos == Character.Position || newPos == targetCharacter.Position)
            return false;

        if (DistanceCache.IntDistance(targetCharacter.Position, newPos) > MonsterBase.Range)
            return false;

        if (Character.Map!.IsTileOccupied(newPos))
            return false;

        if (!Character.Map!.WalkData.HasLineOfSight(Character.Position, newPos))
            return false;

        return Character.TryMove(newPos, 0); //will fail if they can't get there
    }

    /// <summary>
    /// Switches a monster to scan for possible targets.
    /// Always succeeds, a monster can always switch to scan state.
    /// </summary>
    private bool OutSearch()
    {
        nextAiUpdate = Time.ElapsedTimeFloat; //do AI update next tick. Must use elapsed time here for this to work.
        return true;
    }

    /// <summary>
    /// Attempts to start a move towards the chosen target.
    /// </summary>
    private bool OutStartChase()
    {
        var targetChar = targetCharacter;
        if (targetChar == null)
            return false;

        timeLastCombat = Time.ElapsedTimeFloat;

        //var distance = Character.Position.DistanceTo(targetChar.Position);
        if (CombatEntity.CanAttackTarget(targetChar))
        {
            //hasTarget = true;
            nextAiUpdate = -1;
            return true;
        }

        if (Character.TryMove(targetChar.Position, 1))
        {
            nextMoveUpdate = 0;
            nextAiUpdate = -1;
            nextAiSkillUpdate = -1;
            //hasTarget = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the monster is able to change targets to the last attacked enemy.
    /// </summary>
    private bool OutChangeTargets()
    {
        if (Character.LastAttacked.IsNull() || !Character.LastAttacked.IsAlive())
            return false;

        var targetChar = Character.LastAttacked.Get<WorldObject>();
        if (!targetChar.IsActive)
            return false;

        var distance = Character.Position.DistanceTo(targetChar.Position);
        if (distance <= MonsterBase.Range)
        {
            Target = Character.LastAttacked;
            return true;
        }

        if (Character.TryMove(targetChar.Position, 1))
        {

            nextMoveUpdate = 0;
            Target = Character.LastAttacked;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Performs an attack on the monster's currently chosen target. The validity of the attack is assumed to have been checked prior.
    /// </summary>
    private bool OutPerformAttack()
    {
        Debug.Assert(targetCharacter != null, $"Monster {Character.Name} must have a target to use this action.");

        if (CombatEntity.IsCasting || Character.QueuedAction == QueuedAction.Cast || Character.State == CharacterState.Dead)
            return false;

        var targetEntity = targetCharacter.Entity.Get<CombatEntity>();
        if (!targetEntity.IsValidTarget(CombatEntity))
            return false;

        if (AiSkillScanUpdate())
            return true;

        CombatEntity.PerformMeleeAttack(targetEntity);
        Character.QueuedAction = QueuedAction.None;
        timeLastCombat = Time.ElapsedTimeFloat;

        //we should have our cooldown set by PerformMeleeAttack actually
        //nextAiUpdate += MonsterBase.AttackTime;

        return true;
    }

    /// <summary>
    /// Checks if it can attack a target, stops moving immediately and schedules a new AI update next frame.
    /// </summary>
    private bool OutTryAttacking()
    {
        if (!InAttackRange())
            return false;

        return OutStartAttacking();
    }

    /// <summary>
    /// The monster will stop moving immediately and schedule a new AI update next frame.
    /// </summary>
    private bool OutStartAttacking()
    {
        if (Character.IsMoving)
            Character.StopMovingImmediately();
        //{
        //    if (Character.StepsRemaining <= 1) //we're already stopping so we can just bail here.
        //        return false;
        //    //ServerLogger.Debug($"Monster {MonsterBase.Name} {Entity} stopping to attack. Current position {Character.Position} time to stop: {Character.MoveCooldown}");
        //    //Character.ShortenMovePath();

        //    nextAiUpdate = Time.ElapsedTimeFloat + Character.TimeToReachNextStep;
        //    return false;
        //}

        nextAiUpdate = Time.ElapsedTimeFloat;
        nextAiSkillUpdate = nextAiUpdate;
        return true;
    }

    /// <summary>
    /// Revives a monster somewhere based on it's specified respawn rules.
    /// </summary>
    private bool OutTryRevival()
    {
        return World.Instance.RespawnMonster(this);
    }

    private bool OutMoveToOwner()
    {
        var target = Master.Get<WorldObject>().Position;

        var area = Area.CreateAroundPoint(target, 2);
        if (FindRandomMoveTargetInArea(area, target, 2, 4))
            return true;

        if (Character.TryMove(target, 0))
            return true;

        return false;
    }

    private bool OutMoveToItem()
    {
        if (Character.Position == Character.TargetPosition)
            return true;
        if (Character.TryMove(Character.TargetPosition, 0))
            return true;

        return false;
    }

    private bool OutStopMoving()
    {
        Character.ShortenMovePath();
        nextMoveUpdate = Time.ElapsedTimeFloat + GameRandom.NextFloat(4f, 6f);
        return true;
    }

    private bool OutPickUpItem()
    {
        if (!Character.Map!.TryGetGroundItemByDropId(Character.ItemTarget, out var item))
            return false;

        Character.Map!.PickUpOrRemoveItem(Character, item.Id);

        AddItemToInventory(item.ToItemReference());
        nextMoveUpdate = Time.ElapsedTimeFloat + GameRandom.NextFloat(4f, 6f);

        return true;
    }

    #endregion
}