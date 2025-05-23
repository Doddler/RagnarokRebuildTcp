using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class Particle3DSplineData : IResettable
    {
        public RoSpriteData SpriteData;
        public Sprite Sprite;
        public Vector3 Position;
        public Vector2 Velocity;
        public Vector2 MinVelocity;
        public Vector3 MaxVelocity;
        public Vector2 Acceleration;
        public Quaternion Rotation;
        public float Size;
        public int AnimTime;
        public int AnimOffset;
        public bool DoShrink;
        public bool CapVelocity;
        public Color32 Color = new Color32(255,  255, 255, 255);

        public Particle3DSplineData()
        {
            Reset();
        }
        
        public void Reset()
        {
            SpriteData = null;
            Sprite = null;
            Rotation = Quaternion.identity;
            Position = Vector3.zero;
            Velocity = Vector3.zero;
            Acceleration = Vector2.zero;
            MinVelocity = new Vector2(-9999, -9999);
            MaxVelocity = new Vector2(9999, 9999);
            Color = new Color32(255, 255, 255, 255);
            Size = 1f;
            AnimTime = 1;
            AnimOffset = 0;
            CapVelocity = false;
            DoShrink = true;
        }
    }
}