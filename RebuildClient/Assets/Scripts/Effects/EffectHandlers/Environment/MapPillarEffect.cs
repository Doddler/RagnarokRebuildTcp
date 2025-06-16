using Assets.Scripts.Effects.PrimitiveHandlers;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Environment
{
    [RoEffect("MapPillar")]
    public class MapPillarEffect : IEffectHandler
    {
        public static Ragnarok3dEffect CreateJunoPillar(Vector3 position, float size, EffectMaterialType matType)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.MapPillar);
            effect.SetDurationByTime(float.MaxValue);
            effect.transform.position = position; // + new Vector3(0f, 10f, 0f);
            
            var mat = EffectSharedMaterialManager.GetMaterial(matType);
            
            
            var prim = effect.LaunchPrimitive(PrimitiveType.Casting3D, mat, float.MaxValue);
            prim.UpdateHandler = CastingCylinderPrimitive.UpdateMapPillar;
            prim.transform.localScale = new Vector3(2f, 2f, 2f); //Vector3.one; // new Vector3(0.2f, 0.2f, 0.2f);

            prim.CreateParts(4);

            for (var i = 0; i < 4; i++)
            {
                var part = prim.Parts[i];

                part.Active = true;
                part.Step = i * 30;
                part.CoverAngle = 360;
                part.MaxHeight = 120f;
                part.Angle = i * 90;
                part.Alpha = 50;
                part.Distance = size + i * 0.5f;
                part.RiseAngle = 89;
            }

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step > 999)
            {
                effect.ResetStep();
                var prim = effect.Primitives[0];
                prim.SetFrame(0);
                for (var i = 0; i < 4; i++)
                {
                    var part = effect.Primitives[0].Parts[i];

                    part.Step = i * 30;
                    part.Angle = i * 90;
                    part.MaxHeight = 120f;
                }
            }
            
            return effect.IsTimerActive;
        }
    }
}