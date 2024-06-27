namespace RoRebuildServer.Simulation.StatusEffects.Setup;

[Flags]
public enum StatusUpdateMode
{
    None = 0,
    OnTakeDamage = 1,
    OnDealDamage = 2,
}