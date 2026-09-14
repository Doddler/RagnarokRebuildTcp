using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Aura3")]
    public class Aura3Primitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => Update3DCasting3;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => Render3DCasting2;

        //grand cross particularly
        private static void Update3DCasting3(RagnarokPrimitive primitive)
        {
            var mid = (EffectPart.SegmentCount - 1) / 2;
            var m2 = 90 / mid;

            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];

                if (!p.Active)
                    continue;

                if (primitive.IsStepFrame)
                {
                    p.Distance -= 0.01f / 5f;

                    if (primitive.Step >= 10)
                    {
                        p.Alpha -= 1;
                        if (p.Alpha < 0)
                        {
                            p.Alpha = 0;
                            if (p.Step > 30)
                                p.Active = false;

                            p.Distance = 20f / 5f;
                            p.RiseAngle = 90;
                        }
                    }
                    else
                    {
                        p.Alpha += 10f;
                    }

                    for (var i = 0; i < EffectPart.SegmentCount; i++)
                        p.Heights[i] = p.MaxHeight * Mathf.Sin(primitive.Step * Mathf.Deg2Rad);
                }
            }

            primitive.IsDirty = primitive.IsStepFrame;
        }

        private static void Render3DCasting2(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();

            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];
                if (!p.Active)
                    continue;

                var pos = 0;
                var baseAngle = p.CoverAngle / 9f;
                var segmentCount = (int)(p.CoverAngle / baseAngle);

                //Debug.Log(baseAngle);

                var bottomLast = Vector3.zero;
                var topLast = Vector3.zero;

                var color = new Color32(p.Color.r, p.Color.g, p.Color.b, (byte)Mathf.Clamp((int)p.Alpha, 0, 255));

                for (var i = 0f; i <= p.CoverAngle; i += baseAngle)
                {
                    var angle = i + p.RotStart;
                    if (angle > 360)
                        angle -= 360;

                    if (p.CoverAngle >= 359.9f)
                    {
                        if (pos == EffectPart.SegmentCount - 1)
                            angle = p.Angle;
                    }

                    var c1 = Mathf.Cos(angle * Mathf.Deg2Rad);
                    var s1 = Mathf.Sin(angle * Mathf.Deg2Rad);

                    var bottom = new Vector3(c1 * p.Distance, 0f, s1 * p.Distance) + new Vector3(p.Flags[0], 0f, p.Flags[1]);

                    var rx = Mathf.Cos(p.RiseAngle * Mathf.Deg2Rad) * p.Heights[pos];
                    var ry = Mathf.Sin(p.RiseAngle * Mathf.Deg2Rad) * p.Heights[pos];

                    var rz = s1 * rx;
                    rx = c1 * rx;

                    var top = new Vector3(bottom.x + rx, ry, bottom.z + rz);

                    if (pos > 0)
                    {
                        primitive.AddTexturedSliceQuad(topLast, top, bottomLast, bottom, pos, segmentCount, color, 0.2f);
                    }

                    pos++;
                    if (pos >= EffectPart.SegmentCount)
                        pos = EffectPart.SegmentCount - 1;

                    bottomLast = bottom;
                    topLast = top;
                }
            }
        }
    }
}