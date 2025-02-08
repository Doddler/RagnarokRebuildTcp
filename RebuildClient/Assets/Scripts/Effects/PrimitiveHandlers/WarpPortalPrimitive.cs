using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("WarpPortal")]
    public class WarpPortalPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => CastingCylinderPrimitive.Render3DCasting;
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimPortal;

        public static void UpdatePrimPortal(RagnarokPrimitive primitive)
        {
            var mid = (EffectPart.SegmentCount - 1) / 2;
            var m2 = 90 / mid;

            var isActive = false;

            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];
                var ctrl = primitive.Parts[3]; //this effect stores values in the 4th part, for some reason

                
                if (!p.Active)
                    continue;

                isActive = true;
                
                if(primitive.IsStepFrame)
                    p.Step++;

                if (p.Step < 0)
                    continue;

                p.Distance += (0.5f * 60) * Time.deltaTime;

                if(p.Step >= 14) // (p.Distance > 7f)
                {
                    p.Alpha -= (15 * 60) * Time.deltaTime;
                    if(p.Step >= 30) // (p.Alpha < 0)
                    {
                        p.Alpha = 0;
                        if (ctrl.Step > primitive.FrameDuration)
                            p.Active = false;
                        else
                        {
                            p.Step = 0;
                            p.Distance = 0;
                        }
                    }
                }

                if (p.Step < 12)
                {
                    p.Alpha += (24 * 60) * Time.deltaTime;
                    if (p.Alpha > 240)
                        p.Alpha = 240;
                }

                for (var i = 0; i < EffectPart.SegmentCount; i++)
                {
                    p.Heights[i] = p.MaxHeight;
                    if (p.Step <= 10)
                    {
                        p.Heights[i] *= Mathf.Sin(p.Step * 9 * Mathf.Deg2Rad);
                    }
                }
                
                // Debug.Log($"Prim[{ec}] ({p.Active}) Step:{primitive.Parts[ec].Step} Distance:{primitive.Parts[ec].Distance} Alpha:{primitive.Parts[ec].Alpha}");
            }
            
            primitive.IsDirty = true;
            primitive.IsActive = isActive;
        }
    }
}