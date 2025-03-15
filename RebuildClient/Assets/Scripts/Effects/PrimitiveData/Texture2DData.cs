using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.U2D;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class Texture2DData : IResettable
    {
        public Vector2 Size;
        public Vector2 Speed;
        public Vector2 Acceleration;
        public Vector2 MaxSize;
        public Vector2 MinSize;
        public Vector2 ScalingSpeed;
        public Vector2 ScalingAccel;
        public Vector2 ChangedScalingSpeed;
        public Sprite Sprite;
        public int ScalingChangeStep;

        public bool PivotFromBottom;
        public float Alpha;
        public float AlphaSpeed;
        public float FadeOutLength;
        public Color Color = Color.white;


        public void Reset()
        {
            Size = Vector2.zero;
            Speed = Vector2.zero;
            Acceleration = Vector2.zero;
            ScalingSpeed = Vector2.zero;
            ScalingAccel = Vector2.zero;
            MinSize = Vector2.zero;
            MaxSize = new Vector2(9999f, 999f);
            Sprite = null;
            ScalingChangeStep = 0;
            PivotFromBottom = false;
            Alpha = 0;
            AlphaSpeed = 0;
            FadeOutLength = 0;
            Color = Color.white;
        }
    }
}