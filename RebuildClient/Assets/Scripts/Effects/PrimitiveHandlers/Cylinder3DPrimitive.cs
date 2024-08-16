using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Cylinder3D", typeof(CylinderData))]
    public class Cylinder3DPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateCylinder;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderCylinder;

        public static void UpdateCylinder(RagnarokPrimitive primitive)
        {
            var data = primitive.GetPrimitiveData<CylinderData>();
            var oldAlpha = data.Alpha;
            var fadeStartTime = primitive.Duration - data.FadeOutLength;
            if (primitive.CurrentPos > fadeStartTime)
                data.Alpha = Mathf.Clamp(data.Alpha - data.MaxAlpha / data.FadeOutLength * Time.deltaTime, 0, data.MaxAlpha);
            else
                data.Alpha = Mathf.Clamp(data.Alpha + data.AlphaSpeed * Time.deltaTime, 0, data.MaxAlpha);

            data.InnerRadiusSpeed += data.InnerRadiusAccel * Time.deltaTime;
            data.InnerRadius += data.InnerRadiusSpeed * Time.deltaTime;
            data.OuterRadiusSpeed += data.OuterRadiusAccel * Time.deltaTime;
            data.OuterRadius += data.OuterRadiusSpeed * Time.deltaTime;

            data.Velocity += data.Velocity.normalized * data.Acceleration * Time.deltaTime;
            primitive.transform.localPosition += data.Velocity * Time.deltaTime;

            primitive.IsDirty = true;
        }

        public static void RenderCylinder(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();
            
            var data = primitive.GetPrimitiveData<CylinderData>();

            var span = 360;
            var v = 0f;

            var color = new Color(1f, 1f, 1f, data.Alpha / 255f);

            var outerSize = data.OuterRadius;
            var innerSize = data.InnerRadius;

            var textureSpan = 0.25f;
            var allowPerspectiveMapping = true;

            for (var i = 0f; i < span; i += data.ArcAngle)
            {
                var c1 = Mathf.Cos(i * Mathf.Deg2Rad);
                var s1 = Mathf.Sin(i * Mathf.Deg2Rad);
                var c2 = Mathf.Cos((i + data.ArcAngle) * Mathf.Deg2Rad);
                var s2 = Mathf.Sin((i + data.ArcAngle) * Mathf.Deg2Rad);

                var inner1 = new Vector3(c1 * innerSize, 0f, s1 * innerSize);
                var inner2 = new Vector3(c2 * innerSize, 0f, s2 * innerSize);

                var point1 = new Vector3(c1 * outerSize, data.Height, s1 * outerSize);
                var point2 = new Vector3(c2 * outerSize, data.Height, s2 * outerSize);

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