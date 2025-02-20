using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Teleport")]
    public class TeleportPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => CastingCylinderPrimitive.Render3DCasting;
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimTeleport;

        public static void UpdatePrimTeleport(RagnarokPrimitive primitive)
        {
            var mid = (EffectPart.SegmentCount - 1) / 2;
            var m2 = 90 / mid;

            var isActive = false;

            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];

                if (!p.Active)
                    continue;

                isActive = true;

                if (primitive.IsStepFrame)
                    p.Step++;

                p.Angle += (5 - ec) * 60 * Time.deltaTime;
                
                // Debug.Log($"{ec} angle {p.Angle}");
                
                if (p.Step < 0)
                    continue;

                if (p.Flags[0] == 0)
                {
                    var pr = primitive.CurrentPos * 60 * 2;
                    if (pr >= 180)
                        p.Flags[0] = 1;
                    else if(pr >= 135)
                    {
                        p.Alpha -= 1 * 60 * Time.deltaTime;
                        if (p.Alpha < 0)
                            p.Alpha = 0;
                    }

                    p.MaxHeight = p.Flags[1] * Mathf.Sin(pr * Mathf.Deg2Rad);
                }
                else
                {
                    p.Alpha -= 10 * 60 * Time.deltaTime;
                    if (p.Alpha < 0)
                        p.Alpha = 0;
                }

                for (var i = 0; i < EffectPart.SegmentCount; i++)
                {
                    float pr;
                    if (ec < 2)
                        pr = primitive.CurrentPos * 60 + ec * 90;
                    else
                        pr = primitive.CurrentPos * 60 * 3 + ec * 90;

                    var sinLimit = 90 + ((i - mid) * m2);
                    
                    var h = p.MaxHeight;
                    p.Heights[i] = h + p.MaxHeight * Mathf.Sin(sinLimit * Mathf.Deg2Rad) * 0.3f * Mathf.Sin(pr * Mathf.Deg2Rad);
                }

                // Debug.Log($"Prim[{ec}] ({p.Active}) Step:{primitive.Parts[ec].Step} FrameTime: {primitive.CurrentFrameTime} Distance:{primitive.Parts[ec].Distance} Alpha:{primitive.Parts[ec].Alpha}");
            }

            primitive.IsDirty = true;
            primitive.IsActive = isActive;
        }
    }
}