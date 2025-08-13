using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Aura")] //originally called PP_GI_1
    public class AuraPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateAura1;

        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => CastingCylinderPrimitive.Render3DCasting;

        public static void UpdateAura1(RagnarokPrimitive primitive)
        {
            var step = primitive.Step;
            
            var mid = (EffectPart.SegmentCount - 1) / 2;
            var m2 = 90 / mid;
            
            var isActive = false;
            
            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];

                if (!p.Active)
                    continue;

                if(primitive.IsStepFrame)
                    p.Step++;

                if (p.Step > 0)
                {
                    p.Distance += (0.07f * 60) * Time.deltaTime;
                    p.RiseAngle -= (1 * 60) * Time.deltaTime;

                    if (p.RiseAngle < 10)
                    {
                        p.RiseAngle = 10;
                        p.Alpha = 0;
                    }

                    if (p.Distance >= 4)
                    {
                        p.Alpha -= (3 * 60) * Time.deltaTime;
                        if (p.Alpha < 0)
                        {
                            p.Alpha = 0;
                            if (p.Step < primitive.FrameDuration - 30)
                            {
                                p.Distance = 4f - 0.63f;
                                p.RiseAngle = 65 + 9;
                            }
                        }
                    }
                    else
                    {
                        p.Alpha += (10 * 60) * Time.deltaTime;
                    }
                }
                
                for (var i = 0; i < EffectPart.SegmentCount; i++)
                {
                    if (p.Flags[i] == 0)
                    {
                        var sinLimit = 90 + ((i - mid) * m2);
                        var pr = 0;
                        if (ec < 2)
                            pr = (p.Step + ec * 90) % 360;
                        else
                            pr = (p.Step * 2 + ec * 90) % 360;

                        var h = p.MaxHeight;
                        p.Heights[i] = h + p.MaxHeight * Mathf.Sin(sinLimit * Mathf.Deg2Rad) * 0.3f * Mathf.Sin(pr * Mathf.Deg2Rad);
                        //
                        // if (primitive.Step <= 90)
                        //     p.Heights[i] = p.MaxHeight * Mathf.Sin(sinLimit * Mathf.Deg2Rad) * Mathf.Sin(primitive.CurrentPos * 60 * Mathf.Deg2Rad);
                        //
                        // if (p.Heights[i] > p.MaxHeight * Mathf.Sin(sinLimit * Mathf.Deg2Rad))
                        // {
                        //     p.Heights[i] = p.MaxHeight * Mathf.Sin(sinLimit * Mathf.Deg2Rad);
                        //     p.Flags[i] = 1;
                        // }
                        //
                        // if (p.Heights[i] < 0)
                        //     p.Heights[i] = 0;
                    }
                }

                if (p.Step > primitive.FrameDuration && p.Alpha <= 0)
                    p.Active = false;
                else
                    isActive = true;
            }

            primitive.IsActive = isActive;
            //Debug.Log($"Step:{primitive.Parts[0].Step} Distance:{primitive.Parts[0].Distance} RiseAngle:{primitive.Parts[0].RiseAngle} Alpha:{primitive.Parts[0].Alpha}");
        }
    }
}