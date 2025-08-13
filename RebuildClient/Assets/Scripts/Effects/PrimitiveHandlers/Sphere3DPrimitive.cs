using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Sphere3D", typeof(CircleData))]
    public class Sphere3DPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderPrimitive;


        public void UpdatePrimitive(RagnarokPrimitive primitive)
        {
            var data = primitive.GetPrimitiveData<CircleData>();
            var oldAlpha = data.Alpha;
            var fadeStartTime = primitive.Duration - data.FadeOutLength;
            if (primitive.CurrentPos > fadeStartTime)
                data.Alpha = Mathf.Clamp(data.Alpha - data.MaxAlpha / data.FadeOutLength * Time.deltaTime, 0, data.MaxAlpha);
            else
                data.Alpha = Mathf.Clamp(data.Alpha + data.AlphaSpeed * Time.deltaTime, 0, data.MaxAlpha);

            // Debug.Log($"{primitive.CurrentPos} fadeStart:{fadeStartTime} alpha:{data.Alpha} alphaSpeed:{data.AlphaSpeed} fadeSpeed:{data.MaxAlpha/data.FadeOutLength}");

            data.RadiusSpeed += data.RadiusAccel * Time.deltaTime;
            data.Radius += data.RadiusSpeed * Time.deltaTime;

            primitive.IsDirty = true;
            primitive.IsActive = primitive.Step < primitive.FrameDuration;
        }

        public static void RenderPrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            if (!primitive.IsActive)
            {
                mb.Clear();
                return;
            }

            var data = primitive.GetPrimitiveData<CircleData>();
            var color = (Color32)new Color(1f, 1f, 1f, data.Alpha / 255f);
            var hasMesh = mb.HasData;
            var angle = 30;

            primitive.SkipClearingMeshBuilder = true; //no need to rebuild the sphere each frame, the mesh won't change, only colors.

            if (!hasMesh)
            {
                var forward = Vector3.forward;

                var incU = 1 / (360f / angle);
                var incV = 1 / (180f / angle);

                var finalVerticalAngle = 90 - angle;
                var finalHorizontalAngle = 360 - angle;

                var u = 0f;
                var v = 0f;

                for (var y = -90; y <= finalVerticalAngle; y += angle)
                {
                    u = 0f;
                    for (var x = 0; x <= finalHorizontalAngle; x += angle)
                    {
                        // var v1 = Quaternion.Euler(x, y + angle, 0) * forward;
                        // var v2 = Quaternion.Euler(x + angle, y + angle, 0) * forward;
                        // var v3 = Quaternion.Euler(x, y, 0) * forward;
                        // var v4 = Quaternion.Euler(x + angle, y, 0) * forward;
                        
                        var v1 = Quaternion.Euler(y + angle, x,  0) * forward;
                        var v2 = Quaternion.Euler(y + angle, x + angle, 0) * forward;
                        var v3 = Quaternion.Euler(y, x, 0) * forward;
                        var v4 = Quaternion.Euler(y, x + angle, 0) * forward;

                        var uv1 = new Vector2(u, v + incV);
                        var uv2 = new Vector2(u + incU, v + incV);
                        var uv3 = new Vector2(u, v);
                        var uv4 = new Vector2(u + incU, v);

                        primitive.AddTexturedQuad(v1, v2, v3, v4, uv1, uv2, uv3, uv4, color);

                        u += incU;
                    }

                    v += incV;
                }
            }
            else
            {
                var m = primitive.GetMesh();
                var a = (byte)Mathf.Clamp(Mathf.RoundToInt(data.Alpha), 0, 255);
                var colors = m.colors32;
                for (var i = 0; i < colors.Length; i++)
                    colors[i].a = a;
                m.colors32 = colors;
                primitive.SetMesh(m);
                primitive.SkipApplyingMeshFromBuilder = true;
            }

            primitive.transform.localScale = new Vector3(data.Radius, data.Radius, data.Radius);
        }
    }
}