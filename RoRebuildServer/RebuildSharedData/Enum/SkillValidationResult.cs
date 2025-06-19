using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum
{
    public enum SkillValidationResult
    {
        Failure,
        Success,
        InvalidTarget,
        OverlappingAreaOfEffect,
        NoLineOfSight,
        IncorrectWeapon,
        IncorrectAmmunition,
        InsufficientSp,
        CannotCreateMore,
        InsufficientItemCount,
        InsufficientZeny,
        UnusableWhileHidden,
        CannotTargetBossMonster,
        ItemAlreadyStolen,
        MemoLocationInvalid,
        MemoLocationUnwalkable,
        TooFarAway,
        TooClose,
        MustBeStandingInWater,
        MissingRequiredItem,
        SkillNotKnown,
        CannotTeleportHere,
        CartRequired
    }
}
