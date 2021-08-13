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

        private Vector3[] verts = new Vector3[4];
        private Vector3[] normals = new Vector3[4];
        private Color[] colors = new Color[4];
        private Vector2[] uvs = new Vector2[4];
        
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

            //they aren't used in rendering at all
            normals[0] = Vector3.up;
            normals[1] = Vector3.up;
            normals[2] = Vector3.up;
            normals[3] = Vector3.up;

            colors[0] = color;
            colors[1] = color;
            colors[2] = color;
            colors[3] = color;

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

                verts[0] = point1;
                verts[1] = point2;
                verts[2] = inner1;
                verts[3] = inner2;

                uvs[0] = new Vector2(v, 1);
                uvs[1] = new Vector2(v, 1);
                uvs[2] = new Vector2(v+0.25f, 0);
                uvs[3] = new Vector2(v+0.25f, 0);

                mb.AddQuad(verts, normals, uvs, colors);

                v += 0.25f;
                if (v > 1f)
                    v -= 1f;
            }

            mf.sharedMesh = mb.ApplyToMesh(mesh);

            IsDirty = false;
        }
    }
}
