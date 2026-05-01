using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.SkillHandlers;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("FirePillar")]
    public class FirePillarEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.FirePillar);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(999999);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;
            
            for (var i = 0; i < 3; i++)
            {
                var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.FirePillar);
                var prim = effect.LaunchPrimitive(PrimitiveType.Cylinder3D, mat, 999999);
                prim.transform.localScale = Vector3.one * 0.2f;

                var data = prim.GetPrimitiveData<CylinderData>();

                data.InnerRadius = 1.5f + (i + 1);
                data.InnerRadiusSpeed = 0;
                data.InnerRadiusAccel = 0;
                data.OuterRadius = 4 + i;
                data.OuterRadiusSpeed = 0;
                data.OuterRadiusAccel = 0;
                data.Height = 2.5f + ((3 - i) * (8 - (i * 2)) + 1);
                data.Alpha = 240;
                data.MaxAlpha = 240;
                data.RotationAxis = Vector3.up;
                data.RotationSpeed = -360f; // 1.5 rotations per second
                data.RotationAcceleration = 0;
            }

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            return effect.IsTimerActive;
        }
    }
}