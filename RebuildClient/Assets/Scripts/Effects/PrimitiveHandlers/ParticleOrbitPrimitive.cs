using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects.PrimitiveHandlers
{
    [RoPrimitive("ParticleOrbit", typeof(ParticleOrbitData))]
    public class ParticleOrbitPrimitive : IPrimitiveHandler
    {
        public RagnarokEffectData.PrimitiveUpdateDelegate GetDefaultUpdateHandler() => UpdatePrimitive;
        public RagnarokEffectData.PrimitiveRenderDelegate GetDefaultRenderHandler() => RenderPrimitive;


        public void UpdatePrimitive(RagnarokPrimitive primitive)
        {
            if (!primitive.IsActive)
                return;

            if (primitive.Step == 0 && primitive.IsStepFrame)
                return; //do nothing first frame
            
            var dat = primitive.GetPrimitiveData<ParticleOrbitData>();
            dat.GravitySpeed += dat.GravityAccel * Time.deltaTime;
            dat.RotationSpeed += dat.RotationAccel * Time.deltaTime;

            dat.Rotation += dat.RotationSpeed * Time.deltaTime;

            for (var i = 0; i < primitive.PartsCount; i++)
            {
                var part = primitive.Parts[i];
                part.Distance += dat.GravitySpeed * Time.deltaTime;
                
                
                if (!(primitive.CurrentPos < primitive.Duration - dat.FadeOutTime))
                {
                    var remaining = primitive.Duration - primitive.CurrentPos;
                    if (remaining < 0)
                        part.Alpha = 0;
                    else
                        part.Alpha -= part.Alpha * Time.deltaTime / remaining;
                }
            }

            primitive.IsActive = primitive.Step < primitive.FrameDuration;
        }

        public static void RenderPrimitive(RagnarokPrimitive primitive, MeshBuilder mb)
        {
            mb.Clear();
            if (!primitive.IsActive)
                return;

            var dat = primitive.GetPrimitiveData<ParticleOrbitData>();
            
            for (var i = 0; i < primitive.PartsCount; i++)
            {
                var p = primitive.Parts[i];
                
                if (!p.Active)
                    continue;

                var a = (int)Mathf.Clamp(p.Alpha, 0, 255);

                var pos = Quaternion.Euler(0, dat.Rotation + p.Angle, 0f) * p.Position + new Vector3(0f, p.Distance, 0f);
                
                // Debug.Log($"ParticleUp primitive {i} (active {p.Active}): {sprite.name} {p.Position} {p.Distance} {p.Alpha}");
                
                primitive.AddTexturedBillboardSpriteWithAngle(dat.Sprite, pos, dat.Size, dat.Size, 0, new Color32(p.Color.r, p.Color.g, p.Color.b, (byte)a));
            }
        }
    }
}
