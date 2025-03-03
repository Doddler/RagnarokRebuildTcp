using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class SpriteParticleData
    {
        public RoSpriteData SpriteData;
        public Vector2 Size;
        public Vector2 ScalingSpeed;
        public Vector2 ScalingAccel;
        public Vector2 MaxSize;
        public Vector2 MinSize;

        public float Alpha;
        public float AlphaSpeed;
        public int Frame;
        public int FrameSpeed;
        
        public Color Color = Color.white;
    }
}