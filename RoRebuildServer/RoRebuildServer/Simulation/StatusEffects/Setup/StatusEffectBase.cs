using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Simulation.StatusEffects.Setup
{
    public abstract class StatusEffectBase
    {
        public virtual StatusUpdateMode UpdateMode { get; set; }
        public virtual bool OnApply(Player p, ref StatusEffectState state) => true;
        public virtual bool OnExpiration(Player p, ref StatusEffectState state) => true;
        public virtual void OnAttack(Player p, ref StatusEffectState state, DamageInfo info) {}
        public virtual void OnTakeDamage(Player p, ref  StatusEffectState state, DamageInfo info) { }
    }
}
