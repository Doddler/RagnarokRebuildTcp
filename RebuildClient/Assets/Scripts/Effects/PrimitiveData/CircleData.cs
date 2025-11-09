using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class CircleData : IResettable
    {
        public float Radius;
        public float RadiusSpeed;
        public float RadiusAccel;
        public float FadeOutLength;
        public float AlphaSpeed;
        public float MaxAlpha;
        public float Alpha;
        public float ArcAngle = 36f;
        public float InnerSize;
        public Color Color;
        public bool FillCircle;
        public int ChangePoint;
        public float ChangeAccel;
        public float ChangeSpeed;
        
        public void Reset()
        {
            ChangePoint = 0;
            ArcAngle = 36f;
            Color = Color.white;
            Radius = 0;
            RadiusSpeed = 0;
            RadiusAccel = 0;
            MaxAlpha = 0f;
            Alpha = 0f;
            InnerSize = 0f;
            FillCircle = true;
        }
    }
}