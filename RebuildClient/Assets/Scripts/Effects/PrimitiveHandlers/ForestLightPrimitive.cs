using System;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("ForestLight")]
    public class ForestLightPrimitive : IPrimitiveHandler
    {
        
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateForestLight;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderForestLight;
        
        public static void UpdateForestLight(RagnarokPrimitive primitive)
        {
            for (var i = 0; i < primitive.PartsCount; i++)
            {
                var part = primitive.Parts[i];

                if (i == 1 || i == 3)
                {
                    var val = (primitive.CurrentPos + part.Step) % 720f;
                    val /= 2f;

                    part.MaxHeight = part.Heights[0] + Mathf.Sin(val) * 0.5f;
                }
            }

            primitive.IsDirty = true;
        }

        public static void RenderForestLight(RagnarokPrimitive primitive, MeshBuilder mb)
        {

            for (var j = 0; j < primitive.PartsCount; j++)
            {
                var part = primitive.Parts[j];

                var rad = part.MaxHeight;

                Span<Vector3> vectors = stackalloc Vector3[4];

                for (var i = 0; i <= 5; i++)
                {
                    var angle = i * 72f;
                    angle += part.RotStart;
                    if (angle > 360f)
                        angle -= 360f;

                    var rx = rad * Mathf.Cos(angle * Mathf.Deg2Rad);
                    var rz = rad * Mathf.Sin(angle * Mathf.Deg2Rad);

                    vectors[1] = part.Position + new Vector3(rx, 0, rz);
                    vectors[3] = new Vector3(rx, 0, rz);

                    if (i > 0)
                    {
                        var a = part.Alpha / 255f;
                        primitive.AddTexturedQuad(vectors[0], vectors[1], vectors[2], vectors[3], new Color(0.9f, 1f, 0.9f, a));
                    }

                    vectors[0] = vectors[1];
                    vectors[2] = vectors[3];
                }
            }
        }

    }
}