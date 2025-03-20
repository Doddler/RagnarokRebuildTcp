using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("MagnumBreak")]
    public class MagnumBreakEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Attach(ServerControllable target, float motionTime)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.MagnumBreak);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByFrames(32);
            effect.DestroyOnTargetLost = true;
            effect.PositionOffset = new Vector3(0f, 0.35f, 0f);
            effect.ActiveDelay = Mathf.Clamp(motionTime - 0.25f, 0, 0.3f);
            var duration = 0.5f;
            
            var prim = effect.LaunchPrimitive(PrimitiveType.Circle, EffectSharedMaterialManager.GetMaterial(EffectMaterialType.FireRing), 0.5f);
            var data = prim.GetPrimitiveData<CircleData>();
            prim.transform.localScale = new Vector3(1f, 1f, 1f);
            data.Alpha = 0f;
            data.MaxAlpha = 255;
            data.AlphaSpeed = data.MaxAlpha / 0.25f;
            data.FadeOutLength = 0.25f;
            data.InnerSize = 12f / 5f;
            data.Radius = 0f;
            data.RadiusSpeed = 1.75f / 5f * 60f;
            data.RadiusAccel = -(data.RadiusSpeed / duration) / 2f;
            
            prim = effect.LaunchPrimitive(PrimitiveType.Circle, EffectSharedMaterialManager.GetMaterial(EffectMaterialType.FireRing), 0.5f);
            data = prim.GetPrimitiveData<CircleData>();
            prim.transform.localScale = new Vector3(1f, 1f, 1f);
            data.Alpha = 0f;
            data.MaxAlpha = 255;
            data.AlphaSpeed = data.MaxAlpha / 0.25f;
            data.FadeOutLength = 0.25f;
            data.InnerSize = 12f / 5f;
            data.Radius = 0f;
            data.RadiusSpeed = 1.75f / 5f * 60f;
            data.RadiusAccel = -(data.RadiusSpeed / duration) / 2f;
            
            prim = effect.LaunchPrimitive(PrimitiveType.Sphere3D, EffectSharedMaterialManager.GetMaterial(EffectMaterialType.MagnumBreak), 0.5f);
            data = prim.GetPrimitiveData<CircleData>();
            prim.transform.localScale = new Vector3(1f, 1f, 1f);
            prim.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            data.Alpha = 0f;
            data.MaxAlpha = 180;
            data.AlphaSpeed = data.MaxAlpha / 0.25f;
            data.FadeOutLength = 0.25f;
            data.Radius = 0f;
            data.RadiusSpeed = 1.15f / 5f * 60f;
            data.RadiusAccel = -(data.RadiusSpeed / duration) / 2f;
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            for (var i = 0; i < effect.Primitives.Count; i++)
            {
                var p = effect.Primitives[i];
                if (p.PrimitiveType != PrimitiveType.Sphere3D)
                    continue;
                p.transform.localRotation *= Quaternion.Euler(0, Time.deltaTime * 180f, 0);
            }
            
            return effect.IsTimerActive;
        }
    }
}