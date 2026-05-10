using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Blacksmith
{
    [RoEffect("OverThrust")]
    public class OverThrustEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.OverThrust);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(1.5f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;
            
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.CircleMaterialCenter);

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0)
            {
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntity.Id, "black_overthrust.ogg", effect.transform.position, 0.7f);
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntity.Id, "ef_stonecurse.ogg", effect.transform.position, 0.7f);
                
                var circlePrim = effect.LaunchPrimitive(PrimitiveType.Circle2D, effect.Material, 0.667f);
                var cData = circlePrim.GetPrimitiveData<CircleData>();
                
                var scale = 0.025f * 0.75f;

                circlePrim.transform.localScale = new Vector3(scale, scale, scale);
                circlePrim.transform.localPosition += new Vector3(0f, 2f, -0f);
                circlePrim.SetBillboardMode(BillboardStyle.Normal);
                cData.Alpha = 250;
                cData.MaxAlpha = 250;
                cData.FadeOutLength = 0.5f;
                cData.Radius = 10f;
                cData.InnerSize = 10f;
                cData.ArcAngle = 10f;
                cData.RadiusSpeed = 7f * 60f;
                cData.RadiusAccel = -(cData.RadiusSpeed / 0.5f) / 2f;
                cData.FillCircle = false;
                cData.Color = Color.white;
            }
            
            return effect.IsTimerActive;
        }
    }
}