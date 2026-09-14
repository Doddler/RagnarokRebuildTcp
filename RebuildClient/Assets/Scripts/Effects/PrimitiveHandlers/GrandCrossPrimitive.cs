using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("GrandCross")]
    public class GrandCrossPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateGrandCross;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderGrandCross;

        public static void UpdateGrandCross(RagnarokPrimitive primitive)
        {
            var step = primitive.Step;

            //currently forced 60fps... maybe someone can decouple that

            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];

                if (!p.Active)
                    continue;

                if (primitive.IsStepFrame)
                    p.Step++;

                if (primitive.IsStepFrame)
                {
                    if (step > 10)
                    {
                        p.Alpha -= 1f;
                        if (p.Alpha <= 0)
                        {
                            p.Alpha = 0;
                            if (step > 30)
                                p.Active = false;
                        }
                    }
                    else
                        p.Alpha += 9f;

                    p.Heights[0] += 0.001f;
                    p.Heights[1] += 0.001f;
                    p.Heights[2] = p.MaxHeight * Mathf.Sin(step * Mathf.Deg2Rad);
                }
            }
        }

        public static void RenderGrandCross(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            if (!primitive.IsActive)
                return;

            for (var ec = 0; ec < primitive.PartsCount; ec++)
            {
                var p = primitive.Parts[ec];

                if (!p.Active)
                    continue;

                var color = new Color(1f, 1f, 1f, Mathf.Clamp(p.Alpha / 255f, 0, 1));

                var rot = Quaternion.Euler(0, p.RotStart, 0);

                var x = p.Heights[0];
                var z = p.Heights[1];
                var y = 1 / 5f;

                var c1 = rot * new Vector3(-x, y, -z); //0
                var c2 = rot * new Vector3(x, y, -z); //1
                var c3 = rot * new Vector3(x, y, z); //2
                var c4 = rot * new Vector3(-x, y, z); //3

                var v1 = new Vector3(c1.x, p.Heights[2], c1.z);
                var v2 = new Vector3(c2.x, p.Heights[2], c2.z);
                var v3 = new Vector3(c3.x, p.Heights[2], c3.z);
                var v4 = new Vector3(c4.x, p.Heights[2], c4.z);

                primitive.AddTexturedQuad(v1, v2, c1, c2, color);
                primitive.AddTexturedQuad(v2, v3, c2, c3, color);
                primitive.AddTexturedQuad(v3, v4, c3, c4, color);
                primitive.AddTexturedQuad(v4, v1, c4, c1, color);

                var color2 = new Color(color.r, color.g, color.b, color.a / 2f);

                primitive.AddTexturedQuad(c2, c3, c1, c4, color2);
                primitive.AddTexturedQuad(c1, c4, c2, c3, color2);
            }
        }
    }
}