using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.StatusEffects.Setup
{
    public struct StatusEffectState : IEquatable<StatusEffectState>
    {
        public float Expiration;
        public int Value1;
        public int Value2;
        public CharacterStatusEffect Type;

        public static StatusEffectState NewStatusEffect(CharacterStatusEffect type, float expiration, int val1 = 0, int val2 = 0)
        {
            var statusEffect = new StatusEffectState()
            {
                Type = type,
                Expiration = expiration >= 0 ? expiration + Time.ElapsedTimeFloat : float.MaxValue,
                Value1 = val1,
                Value2 = val2,
            };
            return statusEffect;
        }

        public bool Equals(StatusEffectState other)
        {
            return Type.Equals(other.Type);
        }

        public override bool Equals(object? obj)
        {
            return obj is StatusEffectState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type);
        }
    }
}
