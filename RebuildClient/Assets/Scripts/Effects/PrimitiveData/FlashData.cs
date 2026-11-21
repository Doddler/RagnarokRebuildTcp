using System;
using Assets.Scripts.Utility;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public struct FlashRay
    {
        public float ArcLength;
        public float RotationAngle;
        public float RotationSpeed;
        public float RotationAccel;
        public float FadeOutLength;
        public float AlphaSpeed;
        public float MaxAlpha;
        public float Alpha;
        public float Length;
        public float LengthSpeed;
        public int ChangePoint;
        public float ChangeRotationSpeed;
        public float ChangeRotationAccel;
        public float ChangeLengthSpeed;
    }

    //all rays of one flash share the primitive's mesh
    public class FlashData : IResettable
    {
        public FlashRay[] Rays = Array.Empty<FlashRay>();
        public int RayCount;

        public ref FlashRay AddRay()
        {
            if (RayCount == Rays.Length)
                Array.Resize(ref Rays, Math.Max(8, Rays.Length * 2));

            ref var ray = ref Rays[RayCount++];
            ray = default;
            return ref ray;
        }

        public void Reset()
        {
            RayCount = 0;
        }
    }
}