using RebuildSharedData.Enum;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.StatusEffects.Setup
{
    public struct StatusEffectState : IEquatable<StatusEffectState>
    {
        public float Expiration;
        public int Value1;
        public int Value2;
        public short Value3;
        public byte Value4;
        public CharacterStatusEffect Type;

        public static StatusEffectState NewStatusEffect(CharacterStatusEffect type, float expiration, int val1 = 0, int val2 = 0, short val3 = 0, byte val4 = 0)
        {
            var statusEffect = new StatusEffectState()
            {
                Type = type,
                Expiration = expiration >= 0 ? expiration + Time.ElapsedTimeFloat : float.MaxValue,
                Value1 = val1,
                Value2 = val2,
                Value3 = val3,
                Value4 = val4,
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
