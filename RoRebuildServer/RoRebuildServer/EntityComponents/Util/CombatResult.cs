using RebuildSharedData.Enum;

namespace RoRebuildServer.EntityComponents.Util
{
    public record struct CombatResult(AttackResult Result, int Damage);
}