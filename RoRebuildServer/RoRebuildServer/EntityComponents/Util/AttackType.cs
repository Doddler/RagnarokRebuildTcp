namespace RoRebuildServer.EntityComponents.Util
{
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
