using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;
using static Assets.Scripts.Effects.RagnarokEffectData;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Circle", typeof(CircleData))]
    public class CirclePrimitive : IPrimitiveHandler
    {
        public PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateCircle;
        public PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderCircle;
        
        public static void UpdateCircle(RagnarokPrimitive primitive)
        {
            var data = primitive.GetPrimitiveData<CircleData>();
            var oldAlpha = data.Alpha;
            data.Alpha = Mathf.Clamp(data.Alpha + data.AlphaSpeed * 60 + Time.deltaTime, 0, data.MaxAlpha);
            if (!Mathf.Approximately(oldAlpha, data.Alpha))
                primitive.IsDirty = true;
        }

        public static void RenderCircle(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            var data = primitive.GetPrimitiveData<CircleData>();
            
            var span = 360;
            var v = 0f;
            
            var color = new Color(1f, 1f, 1f, data.Alpha / 255f);
            
            for (var i = 0f; i < span; i += data.ArcAngle)
            {
                var c1 = Mathf.Cos(i * Mathf.Deg2Rad);
                var s1 = Mathf.Sin(i * Mathf.Deg2Rad);
                var c2 = Mathf.Cos((i + data.ArcAngle) * Mathf.Deg2Rad);
                var s2 = Mathf.Sin((i + data.ArcAngle) * Mathf.Deg2Rad);

                var inner1 = new Vector3(c1 * data.InnerSize, 0f, s1 * data.InnerSize);
                var inner2 = new Vector3(c2 * data.InnerSize, 0f, s2 * data.InnerSize);

                var point1 = new Vector3(c1 * data.Radius, 0f, s1 * data.Radius);
                var point2 = new Vector3(c2 * data.Radius, 0f, s2 * data.Radius);

                var uv0 = new Vector2(v, 1);
                var uv1 = new Vector2(v, 1);
                var uv2 = new Vector2(v + 0.25f, 0);
                var uv3 = new Vector2(v + 0.25f, 0);
                
                v += 0.25f;
                if (v > 1f)
                    v -= 1f;

                primitive.AddTexturedQuad(point1, point2, inner1, inner2, uv0, uv1, uv2, uv3, color);
            }

        }
    }
}