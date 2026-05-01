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
        LuckyDodge,
        Invisible, //no damage shown and client skill handler
        InvisibleMiss //no damage shown, but attack motion performed
    }
}