using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Objects
{
    [Serializable]
    public class StrAnimationFile : ScriptableObject
    {
        public int FrameRate;
        public int MaxKey;
        public int LayerCount;
        public List<StrLayer> Layers;
        public Texture2D Atlas;
        public Rect[] AtlasRects;
    }

    [Serializable]
    public class StrLayer
    {
        public int TextureCount;
        public List<int> Textures;
        public int AnimationCount;
        public List<StrAnimationEntry> Animations;
    }

    [Serializable]
    public class StrAnimationEntry
    {
        public int Frame;
        public int Type;
        public Vector2 Position;
        public Vector2[] UVs;
        public Vector2[] XY;
        public float Aniframe;
        public int Anitype;
        public float Delay;
        public float Angle;
        public Color Color;
        public float SrcAlpha;
        public float DstAlpha;
        public float MTPreset;
    }

}
