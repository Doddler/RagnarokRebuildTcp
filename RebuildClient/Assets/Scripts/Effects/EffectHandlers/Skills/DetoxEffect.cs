using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("Detox")]
    public class DetoxEffect: IEffectHandler
    {
        public static void LaunchEffect(ServerControllable source)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Detox);
            effect.SourceEntity = source;
            effect.UpdateOnlyOnFrameChange = true;
            effect.SetDurationByFrames(180);
            effect.FollowTarget = source.gameObject;
            effect.DestroyOnTargetLost = false;
            effect.transform.position = source.transform.position;
            effect.transform.localScale = Vector3.one;
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.ParticleAdditive);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step > 100 && effect.Primitives.Count == 0)
                return false;
            
            if (step < 100 && step % 5 == 0)
            {
                var duration = 40 / 60f;
                var size = Random.Range(0.3f, 0.6f);
                
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture3D, effect.Material, duration);
                prim.SetBillboardMode(BillboardStyle.Normal);
                var data = prim.GetPrimitiveData<Texture3DData>();

                var offset = VectorHelper.RandomPositionInCylinder(2f, 6f) / 5f;

                data.Sprite = EffectSharedMaterialManager.GetParticleSprite("particle2");
                data.Size = new Vector2(3f, 3f);
                data.ScalingSpeed = Vector3.zero;
                data.Alpha = 0;
                
                data.AlphaMax = 250f;
                data.AlphaSpeed = data.AlphaMax / 10 * 60; //10 frames
                data.Size = new Vector2(size, size);
                data.MinSize = data.Size;
                data.MaxSize = data.Size;
                data.IsStandingQuad = true;
                data.FadeOutTime = 30 / 60f;
                data.Color = Color.white;
                data.AngleSpeed = 3f * 60f * Mathf.Deg2Rad;
                prim.Velocity = new Vector3(0f, Random.Range(0.4f, 0.9f) / 5f * 60f, 0f);
                prim.Acceleration = -(prim.Velocity / duration) / 2f;
                prim.transform.localPosition = offset;
            }

            return effect.IsTimerActive;
        }
    }
}