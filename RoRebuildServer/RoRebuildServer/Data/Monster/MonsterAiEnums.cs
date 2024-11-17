namespace RoRebuildServer.Data.Monster;

public enum MonsterAiType : byte
{
    AiEmpty,
    AiDebug,
    AiPassive,
    AiPassiveSense,
    AiPassiveImmobile,
    AiAggressive,
    AiAggressiveSense,
    AiAggressiveActiveSense,
    AiAggressiveImmobile,
    AiAggressiveAssist,
    AiAggressiveLooter,
    AiAngry,
    AiLooter,
    AiLooterAssist,
    AiLooterSense,
    AiAssist,
    AiStandardBoss,
    AiPlant,
    AiPacifist,
    AiHyperPacifist,
    AiMinion
}

public enum MonsterAiState : byte
{
    StateAny,
    StateIdle,
    StateRandomMove,
    StateMovingToItem,
    StateChase,
    StateAbnormal,
    StateSearch,
    StateAttacking,
    StateAdjust,
    StateDead,
    StateFlee,
    StateHidden,
    StateSpecial,
}

public enum MonsterInputCheck : byte
{
    InWaitEnd,
    InAttacked,
    InAttackedNoSwap,
    InMeleeAttacked,
    InReachedTarget,
    InReachedRandomMoveTarget,
    InAttackRange,
    InAttackRangeAny,
    InChangeNormal,
    InTargetSearch,
    InEnemyOutOfSight,
    InEnemyOutOfAttackRange,
    InAttackDelayEnd,
    InNeedAttackingAdjust,
    InDeadTimeoutEnd,
    InAllyInCombat,
    InOwnerAttacked,
    InOwnerOutOfSight,
    InTargetedForSkill,
    InItemInSight,
    InItemGone,
    InReachedItem,
    InNoCondition,
}

public enum MonsterOutputCheck : byte
{
    OutRandomMoveStart,
    OutWaitStart,
    OutStartChase,
    OutTryAttacking,
    OutStartAttacking,
    OutSearch,
    OutChangeNormal,
    OutPerformAttack,
    OutChangeTargets,
    OutTryRevival,
    OutAttackingAdjust,
    OutWaitForever,
    OutMoveToOwner,
    OutMoveToItem,
    OutStopMoving,
    OutPickUpItem,
    OutDebug
}