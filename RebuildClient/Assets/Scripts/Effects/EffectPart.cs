using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class EffectPart
    {
        public const int PartCount = 16;

        public bool Active;
        public float Alpha;
        public float Alpha2;
        public float Angle;
        public float CoverAngle;
        public float RiseAngle;
        public float MaxHeight;
        public int Step;
        public float Distance;

        public float[] Heights = new float[PartCount];
        public int[] Flags = new int[PartCount];
        
        public void Clear()
        {
            Active = false;
            Alpha = 0;
            Alpha2 = 0;
            Angle = 0f;
            CoverAngle = 0;
            RiseAngle = 0;
            MaxHeight = 0;
            Step = 0;
            Distance = 0;

            for (var i = 0; i < PartCount; i++)
            {
                Heights[i] = 0;
                Flags[i] = 0;
            }
        }
    }
}