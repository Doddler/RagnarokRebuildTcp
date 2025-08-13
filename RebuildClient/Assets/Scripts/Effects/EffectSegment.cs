using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class EffectSegment
    {
        public Vector3 Position;
        public float Alpha;
        public float Size;
        
        public void Clear()
        {
            Position = Vector3.zero;
            Alpha = 0;
            Size = 0;
        }

        public override string ToString() => $"[Segment Pos:{Position} A:{Alpha} S:{Size}]";
    }
}