using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    class PrimitiveCircleEffect : PrimitiveBaseEffect
    {
        public float Radius;
        public float FadeOutStart;
        public float AlphaSpeed;
        public float MaxAlpha;
        public float Alpha;
        public float ArcAngle = 36f;
        public float InnerSize;

        public bool IsDirty;
        
        public static PrimitiveCircleEffect LaunchEffect(GameObject go, Material mat, int partCount, float duration)
        {
            var prim = go.AddComponent<PrimitiveCircleEffect>();
            prim.Init(partCount, mat);
            prim.Duration = duration;

            prim.IsDirty = true;

            return prim;
        }

        public void Update3DCircle()
        {
            var oldAlpha = Alpha;
            Alpha = Mathf.Clamp(Alpha + AlphaSpeed * 60 + Time.deltaTime, 0, MaxAlpha);
            if (!Mathf.Approximately(oldAlpha, Alpha))
                IsDirty = true;
        }

        public void Render3DCircle()
        {
            if (!IsDirty)
                return;

            mb.Clear();

            var span = 360;
            var v = 0f;
            
            var color = new Color(1f, 1f, 1f, Alpha / 255f);
            
            for (var i = 0f; i < span; i += ArcAngle)
            {
                var c1 = Mathf.Cos(i * Mathf.Deg2Rad);
                var s1 = Mathf.Sin(i * Mathf.Deg2Rad);
                var c2 = Mathf.Cos((i + ArcAngle) * Mathf.Deg2Rad);
                var s2 = Mathf.Sin((i + ArcAngle) * Mathf.Deg2Rad);

                var inner1 = new Vector3(c1 * InnerSize, 0f, s1 * InnerSize);
                var inner2 = new Vector3(c2 * InnerSize, 0f, s2 * InnerSize);

                var point1 = new Vector3(c1 * Radius, 0f, s1 * Radius);
                var point2 = new Vector3(c2 * Radius, 0f, s2 * Radius);

                var uv0 = new Vector2(v, 1);
                var uv1 = new Vector2(v, 1);
                var uv2 = new Vector2(v + 0.25f, 0);
                var uv3 = new Vector2(v + 0.25f, 0);
                
                v += 0.25f;
                if (v > 1f)
                    v -= 1f;

                AddTexturedQuad(point1, point2, inner1, inner2, uv0, uv1, uv2, uv3, color);
            }

            mf.sharedMesh = mb.ApplyToMesh(mesh);

            IsDirty = false;
        }
    }
}
