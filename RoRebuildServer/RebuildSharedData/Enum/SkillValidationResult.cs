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
        UnusableWhileHidden
    }
}
