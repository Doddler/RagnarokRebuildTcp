using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Knight
{
    [RoEffect("BowlingBashImpact")]
    public class BowlingBashImpactEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target, float delayTime)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.BowlingBashImpact);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(2f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;
            effect.ActiveDelay = delayTime;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0 || step == 5)
            {
                var duration = (20 - step) / 60f;
                
                var prim = effect.LaunchPrimitive(PrimitiveType.Cylinder3D, EffectSharedMaterialManager.GetMaterial(EffectMaterialType.BluePerspectiveCylinder), duration);
                //prim.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                //prim.transform.position = target + dir / 5f + Vector3.up * 0.5f;
                prim.transform.localScale = Vector3.one * 0.2f;

                var data = prim.GetPrimitiveData<CylinderData>();

                var speed = (0.33f * 60) / 5f;
                
                //data.Velocity = dir * speed;
                //data.Acceleration = -(speed / effect.Duration) / 2f;
                data.Height = 3.5f;
                data.InnerRadius = 3f;
                data.InnerRadiusSpeed = 0.5f * 60f;
                data.InnerRadiusAccel = -0.03f * 60f;
                data.OuterRadius = 8f;
                data.OuterRadiusSpeed = 0.5f * 60f;
                data.OuterRadiusAccel = -0.03f * 60f;
                data.Height = 3.5f;
                data.Alpha = 240 - step * 7;
                data.MaxAlpha = 255;
                data.FadeOutLength = duration / 2f;

            }

            if (step == 0)
            {
                var duration = 50 / 60f;
                var prim = effect.LaunchPrimitive(PrimitiveType.Circle, EffectSharedMaterialManager.GetMaterial(EffectMaterialType.FireRingNoZCheck), duration);
                var data = prim.GetPrimitiveData<CircleData>();
                prim.transform.localScale = new Vector3(1f, 1f, 1f);
                data.Alpha = 45f;
                data.MaxAlpha = 255;
                data.AlphaSpeed = 0f; // data.MaxAlpha / 0.25f;
                data.FadeOutLength = 35 / 60f;
                data.InnerSize = 5f / 5f;
                data.Radius = 8f / 5f;
                data.RadiusSpeed = 0.7f / 5f * 60f;
                data.RadiusAccel = -(data.RadiusSpeed / duration) / 2f;
                data.ChangePoint = 20;
                data.ChangeAccel = 0;
                data.ChangeSpeed = 0.08f / 5f * 60f;
            }
            
            return effect.IsTimerActive;
        }
    }
}