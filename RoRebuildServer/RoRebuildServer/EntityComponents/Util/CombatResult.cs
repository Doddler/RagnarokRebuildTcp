namespace RoRebuildServer.EntityComponents.Util
{
    public enum AttackResult : byte
    {
        NormalDamage,
        CriticalDamage,
        Miss
    }

    public record struct CombatResult(AttackResult Result, int Damage);
}
