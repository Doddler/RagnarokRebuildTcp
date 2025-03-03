using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum
{
    public enum AttackResult : byte
    {
        NormalDamage,
        CriticalDamage,
        Heal,
        Miss,
        Block,
        Success,
        Invisible
    }
}
