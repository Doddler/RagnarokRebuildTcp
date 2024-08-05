using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Simulation.StatusEffects.Setup
{
    public abstract class StatusEffectBase
    {
        public virtual float Duration => 5f;
        public virtual StatusUpdateMode UpdateMode { get; set; }
        public virtual bool TestApplication(CombatEntity ch, float testValue) => true;
        public virtual void OnApply(CombatEntity ch, ref StatusEffectState state) { }
        public virtual void OnExpiration(CombatEntity ch, ref StatusEffectState state) { }
        public virtual void OnAttack(CombatEntity ch, ref StatusEffectState state, DamageInfo info) { }
        public virtual void OnTakeDamage(CombatEntity ch, ref StatusEffectState state, DamageInfo info) { }
    }
}
