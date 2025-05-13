using System.Runtime.Intrinsics.X86;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.StatusEffects.Setup
{
    public struct StatusEffectState : IEquatable<StatusEffectState>
    {
        public double Expiration;
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
                Expiration = expiration >= 0 ? expiration + Time.ElapsedTime : float.MaxValue,
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

        public static StatusEffectState Deserialize(IBinaryMessageReader br)
        {
            var type = (CharacterStatusEffect)br.ReadByte();
            var time = br.ReadFloat();
            var val1 = br.ReadInt32();
            var val2 = br.ReadInt32();
            var val3 = br.ReadInt16();
            var val4 = br.ReadByte();

            return NewStatusEffect(type, time, val1, val2, val3, val4);
        }

        public void Serialize(IBinaryMessageWriter bw)
        {
            bw.Write((byte)Type);
            if(Expiration < float.MaxValue)
                bw.Write((float)(Expiration - Time.ElapsedTime));
            else
                bw.Write(0);
            bw.Write(Value1);
            bw.Write(Value2);
            bw.Write(Value3);
            bw.Write(Value4);
        }
    }
}
