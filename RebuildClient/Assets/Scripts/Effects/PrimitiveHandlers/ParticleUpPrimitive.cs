using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("ParticleUp", typeof(ParticleUpData))]
    public class ParticleUpPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderPrimitive;

        public void UpdatePrimitive(RagnarokPrimitive primitive)
        {
            if (!primitive.IsActive)
                return;
            
            var isActive = false;

            for (var ec = 0; ec < primitive.Parts.Length; ec++)
            {
                var p = primitive.Parts[ec];
                
                if (!p.Active)
                    continue;

                if(primitive.IsStepFrame)
                    p.Step++;

                isActive = true;
                p.RotStart -= 5f * 60 * Time.deltaTime;

                if (p.Step <= 10)
                    p.Alpha += 15 * 60 * Time.deltaTime;
                else
                {
                    if (primitive.IsStepFrame && (p.Step == 35 - ec * 7 || p.Step == 36 - ec * 7))
                        p.Flags[0] = 1;
                    else
                        p.Flags[0] = 0;

                    p.Alpha -= 3 * 60 * Time.deltaTime;
                    if (p.Alpha < 0)
                        p.Active = false;
                }

                p.Position.y += p.RiseAngle * Time.deltaTime; //distance stores velocity I guess
            }

            primitive.IsActive = isActive;
        }

        public static void RenderPrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();
            if (!primitive.IsActive)
                return;

            var dat = primitive.GetPrimitiveData<ParticleUpData>();
            
            for (var i = 0; i < primitive.PartsCount; i++)
            {
                var p = primitive.Parts[i];
                
                if (!p.Active)
                    continue;

                var a = (int)Mathf.Clamp(p.Alpha, 0, 255);
                var sprite = dat.Atlas.GetSprite(dat.SpriteNames[p.Flags[0]]);
                
                // Debug.Log($"ParticleUp primitive {i} (active {p.Active}): {sprite.name} {p.Position} {p.Distance} {p.Alpha}");
                
                primitive.AddTexturedBillboardSpriteWithAngle(sprite, p.Position, p.Distance, p.Distance, p.RotStart, new Color32(p.Color.r, p.Color.g, p.Color.b, (byte)a));
            }
        }
    }
}
