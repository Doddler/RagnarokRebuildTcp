using Assets.Scripts.Network;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Priest
{
    [RoEffect("Sanctuary")]
    public class SanctuaryEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target, bool isMagnus)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Sanctuary);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(999999);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = false;
            effect.Flags[0] = 0;

            Material mat;
            if(isMagnus)
                mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.MagnusExorcismus);
            else
                mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.Sanctuary);

            var prim = effect.LaunchPrimitive(PrimitiveType.RectUp, mat, 9999999f);
            prim.CreateParts(1);
            
            var part = prim.Parts[0];

            part.RiseAngle = Random.Range(0, 360f);
            part.Alpha = isMagnus ? 18f : 120f;
            part.Heights[0] = isMagnus ? 1f : 0.5f; //x radius
            part.Heights[1] = isMagnus ? 1f : 0.5f; //y radius
            part.Heights[2] = 0f;
            part.MaxHeight = isMagnus ? 10f : 3.2f;
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.Primitives.Count == 0)
                return false;
            
            var prim = effect.Primitives[0];
            
            if (effect.Flags[0] == 0 && !effect.FollowTarget)
            {
                prim.FrameDuration = 0;
                effect.Flags[0] = 1;
            }
            return prim.IsActive;
        }
    }
}