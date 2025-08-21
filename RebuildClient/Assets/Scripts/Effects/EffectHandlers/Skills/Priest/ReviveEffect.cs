using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Priest
{
    [RoEffect("Revive")]
    public class ReviveEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Revive);
            effect.FollowTarget = null;
            effect.SourceEntity = target;
            effect.SetDurationByTime(3.2f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = false;

            effect.transform.position = target.transform.position;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.IsStepFrame && step % 25 == 0 && step <= 132)
            {
                var len = step == 0 ? 3.2f : 1f;
                
                var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.FireRing);
                var prim = effect.LaunchPrimitive(PrimitiveType.Cylinder3D, mat, len);
                prim.transform.localScale = Vector3.one * 0.2f;

                var data = prim.GetPrimitiveData<CylinderData>();

                data.Height = 3.5f;
                data.InnerRadius = 7.5f;
                data.InnerRadiusSpeed = -0.12f * 60;
                data.InnerRadiusAccel = 0.003f * 60 * 60;
                data.OuterRadius = 10.5f;
                data.OuterRadiusSpeed = -0.12f * 60;
                data.OuterRadiusAccel = 0.003f * 60 * 60;
                data.Height = 7.5f;
                data.Alpha = 240;
                data.MaxAlpha = 240;
                data.RotationAxis = Vector3.up;
                data.RotationSpeed = 1.5f * 60f;
                data.RotationAcceleration = 0.03f * 60f * 60f;
                data.FadeOutLength = len / 2f;

                if (step == 0)
                {
                    data.InnerRadiusSpeed = 0;
                    data.InnerRadiusAccel = 0;
                    data.OuterRadiusSpeed = 0;
                    data.OuterRadiusAccel = 0;
                    data.FadeOutLength = len / 10f;
                }
            }

            return effect.IsTimerActive;
        }
    }
}