using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum
{
    public enum SkillTarget : byte
    {
        SingleTarget,
        SingleAlly,
        SingleAny,
        AreaTargeted,
        SelfCast
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
