using Assets.Scripts.Utility;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class FlashData : IResettable
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
        
        public void Reset()
        {
            ChangePoint = 0;
        }
    }
}