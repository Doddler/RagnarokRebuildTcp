using System;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveData
{
    public enum Texture2DBatchShape : byte
    {
        //a rectangle, Size is its half width and half height
        Quad,
        //a diamond tapering to points at the top and bottom, Size is its full width and height
        Pointy,
    }

    public struct Texture2DBatchItem
    {
        //null draws the whole texture
        public Sprite Sprite;
        public Texture2DBatchShape Shape;
        //grows up from Position instead of centering on it; Quad only
        public bool PivotFromBottom;
        public Vector2 Position;
        public Vector2 Speed;
        public Vector2 Acceleration;
        public Vector2 Size;
        public Vector2 ScalingSpeed;
        public Vector2 ScalingAccel;
        //degrees, clockwise
        public float Angle;
        //0 to 255
        public float Alpha;
        public float AlphaSpeed;
        //fades to 0 over the end of Duration; 0 keeps the alpha until the item ends
        public float FadeOutLength;
        public float Duration;
    }

    //all items share the primitive's mesh and material, so their sprites must share a texture
    public class Texture2DBatchData : IResettable
    {
        public Texture2DBatchItem[] Items = Array.Empty<Texture2DBatchItem>();
        public int ItemCount;

        public ref Texture2DBatchItem AddItem()
        {
            if (ItemCount == Items.Length)
                Array.Resize(ref Items, Math.Max(8, Items.Length * 2));

            ref var item = ref Items[ItemCount++];
            item = default;
            return ref item;
        }

        public void Reset()
        {
            ItemCount = 0;
        }
    }
}
