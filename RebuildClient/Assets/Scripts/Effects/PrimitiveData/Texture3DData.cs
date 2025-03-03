using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class Texture3DData
    {
        public RoPrimitiveHandlerFlags Flags;

        public Sprite Sprite;
        public Vector2 Size;
        public Vector2 ScalingSpeed;
        public Vector2 ScalingAccel;
        public Vector2 MaxSize;
        public Vector2 MinSize;

        public float Alpha = 1;
        public float AlphaSpeed = 0;
        public Color32 Color = new Color32(255, 255, 255, 255);
        public Vector4 ColorChange = Vector4.zero;
        public bool IsStandingQuad;

        public float FadeOutTime;
        public float RGBCycleDelay = 0f; //how long to wait after a color
        public float CurCycleDelay = 0f;

        public override string ToString()
        {
            return $"Texture3DData[Size:{Size} Color:{Color}]";
        }
    }
}