namespace RoRebuildServer.Data.Monster;

public enum MonsterAiType : byte
{
    AiEmpty,
    AiDebug,
    AiPassive,
    AiPassiveImmobile,
    AiAggressive,
    AiAggressiveImmobile,
    AiLooter,
    AiAssist,
    AiAssistLooter,
    AiAggressiveLooter,
    AiAngry,
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
    StateChase,
    StateAbnormal,
    StateSearch,
    StateAttacking,
    StateAdjust,
    StateDead,
    StateFlee
}

public enum MonsterInputCheck : byte
{
    InWaitEnd,
    InAttacked,
    InAttackedNoSwap,
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
    OutDebug
}