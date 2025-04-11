using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class Texture3DData : IResettable
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
        public float AlphaMax = 0;
        public float Angle;
        public float AngleSpeed;
        public Color Color = UnityEngine.Color.white;
        public Vector4 ColorChange = Vector4.zero;
        public bool IsStandingQuad; //a standing quad is one where y is up instead of z. Used in billboards.

        public float FadeOutTime;
        public float RGBCycleDelay = 0f; //how long to wait after a color
        public float CurCycleDelay = 0f;

        public override string ToString()
        {
            return $"Texture3DData[Size:{Size} Color:{Color}]";
        }

        public void Reset()
        {
            Sprite = null;
            Size = Vector2.zero;
            ScalingSpeed = Vector2.zero;
            ScalingAccel = Vector2.zero;
            MaxSize  = Vector2.zero;
            MinSize = Vector2.zero;
            Alpha = 1;
            AlphaSpeed = 0;
            AlphaMax = 0;
            Angle = 0;
            AngleSpeed = 0;
            Color = Color.white;
            IsStandingQuad = false;
            FadeOutTime = 0;
            Flags = RoPrimitiveHandlerFlags.None;
            //we don't care about resetting the color change stuff, they'll need to set all the values if the color change flag is set
        }
    }
}