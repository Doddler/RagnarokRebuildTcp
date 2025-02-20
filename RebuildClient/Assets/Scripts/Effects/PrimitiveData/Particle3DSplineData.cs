using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class Particle3DSplineData
    {
        public Sprite Sprite;
        public Vector3 Position;
        public Vector2 Velocity;
        public Vector2 Acceleration;
        public Quaternion Rotation;
        public float Size;
        public Color32 Color = new Color32(255,  255, 255, 255);
    }
}