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
        public BillboardStyle Style;
        public string[] SpriteList;
        public int TextureCount;
        public int FrameRate;
        public bool AnimateTexture;
        public Vector3 BaseRotation;
        // public Vector3 Target;
        
        public float Width;
        public float Height;

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
            TextureCount = 0;
            FrameRate = 0;
            AnimateTexture = false;
            BaseRotation = Vector3.zero;
            // Target = Vector3.zero;
            Width = 0;
            Height = 0;
            Alpha = 255;
            MaxAlpha = 255;
            AlphaSpeed = 0;
            FadeOutLength = 0;
            Color = Color.white;
        }
    }
}