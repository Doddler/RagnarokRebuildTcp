using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public class Texture3DData
    {
        public RoPrimitiveHandlerFlags Flags;
        
        public Vector2 Size;
        public Vector2 ScalingSpeed;
        public Vector2 ScalingAccel;
        public Vector2 MaxSize;
        public Vector2 MinSize;
        
        public Color Color = Color.white;
        public Vector4 ColorChange = Vector4.zero;
        
        public float RGBCycleDelay = 0f; //how long to wait after a color
        public float CurCycleDelay = 0f;
        
    }
}