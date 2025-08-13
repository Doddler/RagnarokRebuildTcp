namespace RebuildSharedData.Enum
{
    public enum SkillTarget : byte
    {
        Passive,
        Enemy,
        Ally,
        Any,
        Ground,
        Self,
        Trap
    }

    public enum SkillPreferredTarget : byte
    {
        Any,
        Enemy,
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
    public enum AttackFlags : int
    {
        Neutral = 0,
        Physical = 1 << 1,
        Magical = 1 << 2,
        CanCrit = 1 << 3,
        GuaranteeCrit = 1 << 4,
        CanHarmAllies = 1 << 5,
        IgnoreDefense = 1 << 6,
        IgnoreSubDefense = 1 << 7,
        IgnoreEvasion = 1 << 8,
        IgnoreNullifyingGroundMagic = 1 << 9,
        NoDamageModifiers = 1 << 10,
        NoElement = 1 << 11,
        Ranged = 1 << 12,
        NoTriggerOnAttackEffects = 1 << 13,
        NoTriggerWhenAttackedEffects = 1 << 14,
        CanAttackHidden = 1 << 15,
        IgnoreWeaponRefine = 1 << 16,
        ReverseDefense = 1 << 17,
        NoTriggers = NoTriggerOnAttackEffects | NoTriggerWhenAttackedEffects,
        PhysicalStatusTest = Physical | IgnoreEvasion | IgnoreNullifyingGroundMagic | NoTriggerOnAttackEffects | NoTriggerWhenAttackedEffects | NoElement
    }
}
