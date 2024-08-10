using UnityEngine;
using static Assets.Scripts.Effects.RagnarokEffectData;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("Heal")]
    public class HealPrimitive : IPrimitiveHandler
    {
        public PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdateHealPrimitive;
        public PrimitiveRenderDelegate GetDefaultRenderHandler() => CastingCylinderPrimitive.Render3DCasting;

        public static void UpdateHealPrimitive(RagnarokPrimitive primitive)
        {
            var step = primitive.Step;

            // var mid = (EffectPart.SegmentCount - 1) / 2;
            // var m2 = 90 / mid;
            
            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                //if (Mathf.Approximately(size, 3f) && ec < 3)
                //    ec = 3;

                var p = primitive.Parts[ec];

                if (p.Active)
                {
                    // if (ec < 2)
                    //     p.Angle += (ec + 4) * 60 * Time.deltaTime;
                    // else
                    //     p.Angle += (ec + 2) * 60 * Time.deltaTime;

                    // if (p.Angle > 360)
                    //     p.Angle -= 360;

                    if (step < 16)
                        p.Alpha += p.AlphaRate * 60 * Time.deltaTime;
                    if (step >= p.AlphaTime)
                        p.Alpha -= 2 * 60 * Time.deltaTime;
                }
                
                for (var i = 0; i < EffectPart.SegmentCount; i++)
                {
                    if (p.Flags[i] == 0)
                    {
                        //var sinLimit = 90 + ((i - mid) * m2);
                        var sinLimit = ec switch
                        {
                            0 => 4 * primitive.CurrentPos,
                            1 => 3 * primitive.CurrentPos,
                            _ => 2 * primitive.CurrentPos
                        };

                        var height = p.MaxHeight * 0.75f + p.MaxHeight * 0.25f * Mathf.Sin(sinLimit * Mathf.Deg2Rad); 
                        
                        if (primitive.Step <= 90)
                            p.Heights[i] = height * Mathf.Sin(primitive.CurrentPos * 60 * Mathf.Deg2Rad);

                        if (p.Heights[i] < 0)
                            p.Heights[i] = 0;
                    }
                }
            }

            primitive.IsDirty = true;
        }
    }
}