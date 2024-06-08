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
}
