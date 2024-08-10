using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum
{
    public enum SkillTarget : byte
    {
        Passive,
        Enemy,
        Ally,
        Any,
        Ground,
        Self
    }

    public enum SkillClass : byte
    {
        None,
        Physical,
        Ranged,
        Magic,
        Unique
    }

    [Flags]
    public enum AttackFlags : byte
    {
        Neutral = 0,
        Physical = 1,
        Magical = 2,
        CanCrit = 4,
        CanHarmAllies = 8,
        IgnoreDefense = 16,
    }
}
