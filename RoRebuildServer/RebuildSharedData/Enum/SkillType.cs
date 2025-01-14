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
    public enum AttackFlags : short
    {
        Neutral = 0,
        Physical = 1,
        Magical = 2,
        CanCrit = 4,
        GuaranteeCrit = 8,
        CanHarmAllies = 16,
        IgnoreDefense = 32,
        IgnoreEvasion = 64,
        IgnoreNullifyingGroundMagic = 128,
        NoDamageModifiers = 256,
        NoElement = 512,
        Ranged = 1024,
        NoTriggerOnAttackEffects = 2048,
        NoTriggerWhenAttackedEffects = 4096,
        PhysicalStatusTest = Physical | IgnoreEvasion | IgnoreNullifyingGroundMagic | NoTriggerOnAttackEffects | NoTriggerWhenAttackedEffects | NoElement
    }
}
