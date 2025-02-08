using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Wind")]
    public class WindPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateWindPrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderWindPrimitive;

        private static void UpdateWindPrimitive(RagnarokPrimitive primitive)
        {
            // var mid = (EffectPart.SegmentCount - 1) / 2;
            // var m2 = 90 / mid;

            var isActive = false;
            var step = primitive.Step;

            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];
                if (!p.Active)
                    continue;

                isActive = true;

                p.Angle += (5 * 60) * Time.deltaTime;
                if (p.Angle > 360)
                    p.Angle = 0;

                p.CoverAngle += (3 * 60) * Time.deltaTime;
                if (p.CoverAngle > 120)
                    p.CoverAngle = 120;

                if (step > p.AlphaTime)
                {
                    p.Alpha -= (1.5f * 60) * Time.deltaTime;
                    if (p.Alpha < 0)
                    {
                        p.Alpha = 0;
                        p.Active = false;
                    }
                }
                else
                {
                    p.Alpha += (10 * 60) * Time.deltaTime;
                    if (p.Alpha > 120)
                        p.Alpha = 120;
                }
            }

            primitive.IsActive = isActive;
            primitive.IsDirty = true;
        }

        private static void RenderWindPrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();
            
            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];
                if (!p.Active)
                    continue;

                var baseAngle = p.CoverAngle / (EffectPart.SegmentCount - 1);
                var pos = 0;

                var bottomLast = Vector3.zero;
                var topLast = Vector3.zero;
                
                var color = new Color32(p.Color.r, p.Color.g, p.Color.b, (byte)Mathf.Clamp((int)p.Alpha, 0, 255));

                for (var i = 0f; i <= p.CoverAngle; i += baseAngle)
                {
                    var angle = i + p.Angle;
                    if (angle > 360)
                        angle -= 360;

                    if (p.CoverAngle >= 360)
                    {
                        if (pos == EffectPart.SegmentCount - 1)
                            angle = p.Angle;
                    }

                    var c1 = Mathf.Cos(angle * Mathf.Deg2Rad);
                    var s1 = Mathf.Sin(angle * Mathf.Deg2Rad);

                    var bottom = new Vector3(c1 * p.Distance, p.MaxHeight, s1 * p.Distance);

                    //if (ec == 3)
                    //    bottom.y = -2f;

                    var rx = Mathf.Cos(p.RiseAngle * Mathf.Deg2Rad) * p.Heights[pos];
                    var ry = Mathf.Sin(p.RiseAngle * Mathf.Deg2Rad) * p.Heights[pos] + p.MaxHeight;

                    var rz = s1 * rx;
                    rx = c1 * rx;

                    var top = new Vector3(bottom.x + rx, ry, bottom.z + rz);

                    if (pos > 0)
                    {
                        primitive.AddTexturedSliceQuad(topLast, top, bottomLast, bottom, pos, EffectPart.SegmentCount, color, 0.1f);
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