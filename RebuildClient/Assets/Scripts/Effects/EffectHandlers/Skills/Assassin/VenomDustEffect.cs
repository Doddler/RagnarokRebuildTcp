using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Assassin
{
    [RoEffect("VenomDust")]
    public class VenomDustEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.VenomDust);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(999999);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.ParticleAlphaBlend);

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.IsStepFrame && step % 5 == 0)
            {
                var duration = 10f / 60f;
                var size = 80 / 100f;
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture3D, effect.Material, duration);
                prim.SetBillboardMode(BillboardStyle.Normal);
                var data = prim.GetPrimitiveData<Texture3DData>();

                data.Sprite = EffectSharedMaterialManager.GetParticleSprite("particle3");
                data.ScalingSpeed = Vector3.zero;
                data.Alpha = 70f;
                data.Angle = Random.Range(0, 360f);
                
                data.AlphaMax = 255f;
                data.AlphaSpeed = data.AlphaMax / 10 * 60; //10 frames
                data.Size = new Vector2(size, size);
                data.MinSize = data.Size;
                data.MaxSize = data.Size;
                data.IsStandingQuad = true;
                data.FadeOutTime = 5 / 60f;
                data.Color = Color.white;
                data.AngleSpeed = 3f * 60f * Mathf.Deg2Rad;
                prim.Velocity = new Vector3(0f, Random.Range(1f, 2f) / 16f * 60f, 0f);
                prim.Acceleration = -(prim.Velocity / duration) / 2f;
                prim.transform.localPosition = Vector3.zero;
            }
            
            return effect.IsTimerActive;
        }
    }
}