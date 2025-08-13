using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("CartRevolution")]
    public class CartRevolutionEffect : IEffectHandler
    {
        public static void CreateCartRevolution(ServerControllable target, float motionTime)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.CartRevolution);
            effect.SetDurationByFrames(60);
            effect.UpdateOnlyOnFrameChange = true;
            effect.ActiveDelay = motionTime;
            effect.FollowTarget = target.gameObject;
            effect.SetBillboardMode(BillboardStyle.Character);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0)
            {
                effect.FollowTarget = null;
                CameraFollower.Instance.AttachEffectToEntity("CartRevolution", effect.gameObject);
            }

            if (step == 7 || step == 20)
            {
                AudioManager.Instance.OneShotSoundEffect(-1, "ef_magnumbreak.ogg", effect.transform.position, 0.7f);

                var duration = 0.333f;
                var fadeLen = 0.25f;
                if (step == 20)
                {
                    duration = 0.25f;
                    fadeLen = 0.15f;
                }
                
                var prim = effect.LaunchPrimitive(PrimitiveType.Circle, EffectSharedMaterialManager.GetMaterial(EffectMaterialType.FireRing), duration);
                var data = prim.GetPrimitiveData<CircleData>();
                prim.transform.localScale = new Vector3(1f, 1f, 1f);
                data.Alpha = 0f;
                data.MaxAlpha = 180;
                data.AlphaSpeed = data.MaxAlpha / 0.15f;
                data.FadeOutLength = fadeLen;
                data.InnerSize = 5f / 5f;
                data.Radius = 0f;
                data.RadiusSpeed = 1.75f / 5f * 60f;
                data.RadiusAccel = -(data.RadiusSpeed / duration) / 2f;
            
                prim = effect.LaunchPrimitive(PrimitiveType.Sphere3D, EffectSharedMaterialManager.GetMaterial(EffectMaterialType.MagnumBreak), duration);
                data = prim.GetPrimitiveData<CircleData>();
                prim.transform.localScale = new Vector3(1f, 1f, 1f);
                prim.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                data.Alpha = 0f;
                data.MaxAlpha = 240;
                data.AlphaSpeed = data.MaxAlpha / 0.117f;
                data.FadeOutLength = fadeLen;
                data.Radius = 0f;
                data.RadiusSpeed = 1.35f / 5f * 60f;
                data.RadiusAccel = -(data.RadiusSpeed / duration) / 2f;
            }


            return step < effect.DurationFrames;
        }
    }
}