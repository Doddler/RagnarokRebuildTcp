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
        NoLineOfSight,
        IncorrectWeapon,
        InsufficientSp
    }
}
