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
            var fadeStartTime = primitive.Duration - data.FadeOutLength;
            if (primitive.CurrentPos > fadeStartTime)
                data.Alpha = Mathf.Clamp(data.Alpha - data.MaxAlpha / data.FadeOutLength * Time.deltaTime, 0, data.MaxAlpha);
            else
                data.Alpha = Mathf.Clamp(data.Alpha + data.AlphaSpeed * Time.deltaTime, 0, data.MaxAlpha);

            // Debug.Log($"{primitive.CurrentPos} fadeStart:{fadeStartTime} alpha:{data.Alpha} alphaSpeed:{data.AlphaSpeed} fadeSpeed:{data.MaxAlpha/data.FadeOutLength}");
            
            if (primitive.IsStepFrame && data.ChangePoint > 0 && data.ChangePoint == primitive.Step)
            {
                data.RadiusSpeed = data.ChangeSpeed;
                data.RadiusAccel = data.ChangeAccel;
            }

            data.RadiusSpeed += data.RadiusAccel * Time.deltaTime;
            data.Radius += data.RadiusSpeed * Time.deltaTime;

            primitive.IsDirty = true;
            primitive.IsActive = primitive.Step < primitive.FrameDuration;
        }

        public static void RenderCircle(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            if (!primitive.IsActive)
                return;
            
            var data = primitive.GetPrimitiveData<CircleData>();

            var span = 360;
            var v = 0f;

            var color = new Color(1f, 1f, 1f, data.Alpha / 255f);

            var size = data.InnerSize;
            if (data.FillCircle || data.Radius < data.InnerSize)
                size = data.Radius;

            var innerSize = data.Radius - size;

            var textureSpan = 0.25f;
            var allowPerspectiveMapping = true;

            data.ArcAngle = 36f;

            for (var i = 0f; i < span; i += data.ArcAngle)
            {
                var c1 = Mathf.Cos(i * Mathf.Deg2Rad);
                var s1 = Mathf.Sin(i * Mathf.Deg2Rad);
                var c2 = Mathf.Cos((i + data.ArcAngle) * Mathf.Deg2Rad);
                var s2 = Mathf.Sin((i + data.ArcAngle) * Mathf.Deg2Rad);

                var inner1 = new Vector3(c1 * innerSize, 0f, s1 * innerSize);
                var inner2 = new Vector3(c2 * innerSize, 0f, s2 * innerSize);

                var point1 = new Vector3(c1 * data.Radius, 0f, s1 * data.Radius);
                var point2 = new Vector3(c2 * data.Radius, 0f, s2 * data.Radius);

                if (!allowPerspectiveMapping || Mathf.Approximately(innerSize, 0f))
                {
                    var uv0 = new Vector2(v, 1);
                    var uv1 = new Vector2(v - textureSpan, 1);
                    var uv2 = new Vector2(v, 0);
                    var uv3 = new Vector2(v - textureSpan, 0);

                    v -= textureSpan;
                    if (v < 0f)
                        v += 1f;

                    primitive.AddTexturedQuad(point1, point2, inner1, inner2, uv0, uv1, uv2, uv3, color);
                }
                else
                {
                    //uv correction based on: https://gamedev.stackexchange.com/questions/148082/how-can-i-fix-zig-zagging-uv-mapping-artifacts-on-a-generated-mesh-that-tapers
                    
                    var scale1 = (inner1 - inner2).magnitude;
                    var scale2 = (point1 - point2).magnitude;
                    var scale = scale1 / scale2;

                    scale1 = scale;
                    //scale2 = 1;

                    var uv0 = new Vector3(v, 1, 1);
                    var uv1 = new Vector3(v - textureSpan, 1, 1);
                    var uv2 = new Vector3((v * scale1), 0, scale1);
                    var uv3 = new Vector3((v - textureSpan) * scale1, 0, scale1);

                    v -= textureSpan;
                    if (v < 0f)
                        v += 1f;

                    primitive.AddTexturedPerspectiveQuad(point1, point2, inner1, inner2, uv0, uv1, uv2, uv3, color);
                }
            }
        }
    }
}