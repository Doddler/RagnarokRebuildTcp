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
        Melee = 1 << 13,
        AutoRange = 1 << 14,
        NoTriggerOnAttackEffects = 1 << 15,
        NoTriggerWhenAttackedEffects = 1 << 16,
        CanAttackHidden = 1 << 17,
        IgnoreWeaponRefine = 1 << 18,
        ReverseDefense = 1 << 19,
        OffHandWeapon = 1 << 20,
        NoTriggers = NoTriggerOnAttackEffects | NoTriggerWhenAttackedEffects,
        PhysicalStatusTest = Physical | IgnoreEvasion | IgnoreNullifyingGroundMagic | NoTriggerOnAttackEffects | NoTriggerWhenAttackedEffects | NoElement
    }
}