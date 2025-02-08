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
            var isActive = false;
            
            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                //if (Mathf.Approximately(size, 3f) && ec < 3)
                //    ec = 3;

                var p = primitive.Parts[ec];

                if (p.Active)
                {
                    isActive = true;
                    if (p.Flags[0] == 1) //warp portal
                    {
                        p.Angle += (ec + 4) * 60 * Time.deltaTime;
                        // if (ec < 2)
                        //     p.Angle += (ec + 4) * 60 * Time.deltaTime;
                        // else
                        //     p.Angle += (ec + 2) * 60 * Time.deltaTime;

                        if (p.Angle > 360)
                            p.Angle -= 360;
                    }

                    if (p.Flags[0] == 2) //map entry
                    {
                        if(ec < 2)
                            p.Angle += (ec + 4) * 60 * Time.deltaTime;
                        else
                            p.Angle += (ec + 4) * 60 * Time.deltaTime;
                    }

                    if (step < 16)
                        p.Alpha += p.AlphaRate * 60 * Time.deltaTime;
                    if (step >= p.AlphaTime)
                    {
                        p.Alpha -= 2 * 60 * Time.deltaTime;
                        if (p.Alpha <= 0)
                            p.Active = false;
                    }
                }
                
                for (var i = 0; i < EffectPart.SegmentCount; i++)
                {
                    if (p.Flags[i] == 0)
                    {
                        //var sinLimit = 90 + ((i - mid) * m2);
                        var sinLimit = ec switch
                        {
                            0 => 4 * primitive.CurrentPos * 60,
                            1 => 3 * primitive.CurrentPos * 60,
                            _ => 2 * primitive.CurrentPos * 60
                        };
                        sinLimit += i * 34;
                        sinLimit %= 360;

                        var height = p.MaxHeight * 0.75f + p.MaxHeight * 0.25f * Mathf.Sin(sinLimit * Mathf.Deg2Rad);

                        if (primitive.Step <= 90)
                            p.Heights[i] = height * Mathf.Sin(primitive.CurrentPos * 60 * Mathf.Deg2Rad);
                        else
                            p.Heights[i] = height;

                        if (p.Heights[i] < 0)
                            p.Heights[i] = 0;
                    }

                    if (p.Flags[i] == 1 || p.Flags[i] == 3) //warp portal and, apparently, 'big portal'
                    {
                        var height = p.MaxHeight;
                        if (step < 90)
                            height *= Mathf.Sin(step * Mathf.Deg2Rad);
                        p.Heights[i] = height;
                    }

                    if (p.Flags[i] == 2) //entry
                    {
                        var height = p.MaxHeight;
                        if (step < 45)
                            height *= Mathf.Sin(step * 4 * Mathf.Deg2Rad);
                        else
                            height = 0;
                        p.Heights[i] = height;
                    }
                }
            }

            primitive.IsActive = isActive;
            primitive.IsDirty = true;
        }
    }
}