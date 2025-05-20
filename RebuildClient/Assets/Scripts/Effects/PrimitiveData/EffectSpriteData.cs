using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.U2D;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class EffectSpriteData : IResettable
    {
        public Texture2D Texture;
        public SpriteAtlas Atlas;
        public RoSpriteData SpriteData;
        public BillboardStyle Style;
        public string[] SpriteList;
        public int TextureCount;
        public int FrameTime; //ms per frame
        public int Frame;
        public bool AnimateTexture;
        public Vector3 BaseRotation;
        // public Vector3 Target;
        
        public float Width;
        public float Height;

        public float Angle;
        public float RotationSpeed;
        public float Alpha = 255;
        public float MaxAlpha = 255;
        public float AlphaSpeed = 0;
        public float FadeOutLength = 0;
        public Color Color = UnityEngine.Color.white;
        
        public void Reset()
        {
            Texture = null;
            Atlas = null;
            Style = BillboardStyle.None;
            SpriteList = null;
            SpriteData = null;
            TextureCount = 0;
            FrameTime = 0;
            AnimateTexture = false;
            BaseRotation = Vector3.zero;
            // Target = Vector3.zero;
            Width = 0;
            Height = 0;
            Angle = 0;
            RotationSpeed = 0;
            Alpha = 255;
            MaxAlpha = 255;
            AlphaSpeed = 0;
            FadeOutLength = 0;
            Color = Color.white;
        }
    }
}