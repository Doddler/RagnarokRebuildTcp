using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class CylinderData
    {
        public float InnerRadius;
        public float InnerRadiusSpeed;
        public float InnerRadiusAccel;
        public float OuterRadius;
        public float OuterRadiusSpeed;
        public float OuterRadiusAccel;
        public float Height;
        public float FadeOutLength;
        public float AlphaSpeed;
        public float MaxAlpha;
        public float Alpha;
        public float ArcAngle = 36f;
        public Vector3 Velocity;
        public float Acceleration;
        public Vector3 RotationAxis = Vector3.up;
        public float RotationSpeed;
        public float RotationAcceleration;
    }
}