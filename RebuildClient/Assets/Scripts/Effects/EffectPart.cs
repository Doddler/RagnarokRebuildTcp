using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class EffectPart
    {
        public const int SegmentCount = 21;

        public bool Active;
        public float Alpha;
        public float AlphaTime;
        public float AlphaRate;
        public float Angle;
        public float CoverAngle;
        public float RiseAngle;
        public float MaxHeight;
        public float RotStart;
        public int Step;
        public float Distance;
        public Vector3 Position;
        public Color32 Color = new Color32(255, 255, 255, 255);

        public float[] Heights = new float[SegmentCount];
        public int[] Flags = new int[SegmentCount];
        
        public void Clear()
        {
            Active = false;
            Alpha = 0;
            AlphaTime = 0;
            AlphaRate = 0;
            Angle = 0f;
            CoverAngle = 0;
            RiseAngle = 0;
            MaxHeight = 0;
            Step = 0;
            Distance = 0;
            RotStart = 0;
            Step = 0;
            Color = new Color32(255, 255, 255, 255);
            Position = Vector3.zero;

            for (var i = 0; i < SegmentCount; i++)
            {
                Heights[i] = 0;
                Flags[i] = 0;
            }
        }
    }
}