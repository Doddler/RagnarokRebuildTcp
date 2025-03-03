using Assets.Scripts.Effects.PrimitiveData;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("CastLockOn")]
    public class CastLockOnEffect : IEffectHandler
    {
        private static Material lockOnMaterial;

        public static Ragnarok3dEffect Create(float duration, GameObject followTarget)
        {
            if (duration < 0.15f)
                return null;
            
            if (lockOnMaterial == null)
            {
                lockOnMaterial = new Material(ShaderCache.Instance.AlphaBlendParticleShader);
                lockOnMaterial.mainTexture = Resources.Load<Texture2D>("LockOn128");
                lockOnMaterial.renderQueue = 2998;
            }

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.CastLockOn);
            effect.FollowTarget = followTarget;
            effect.UpdateOnlyOnFrameChange = false;
            effect.SetDurationByTime(duration);
            //effect.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            var angle = 0f;
            for (var i = 0; i < 2; i++)
            {
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture3D, lockOnMaterial, duration);
                var data = prim.GetPrimitiveData<Texture3DData>();
                prim.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                prim.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                data.Size = new Vector2(30 / 5f, 30 / 5f);
                data.ScalingSpeed = new Vector2(-60f / 5f, -60f / 5f);
                data.MaxSize = data.Size;
                data.MinSize = new Vector2(20 / 10f, 20 / 10f);
                data.Flags = RoPrimitiveHandlerFlags.CycleColors;
                data.Color = new Color(250 / 255f, 150 / 255f, 150 / 255f, 1f);
                data.ColorChange = new Vector4(0f, 1.5f, 1.5f, 0f);
                data.RGBCycleDelay = 20f / 60f;

                angle += 45f;
            }

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            var primitives = effect.GetPrimitives;
            for (var i = 0; i < primitives.Count; i++)
            {
                primitives[i].gameObject.transform.Rotate(new Vector3(0f, 270f * Time.deltaTime, 0f), Space.Self);
            }

            // Debug.Log($"{step} {effect.DurationFrames}");

            if (step >= effect.DurationFrames)
            {
                foreach (var e in effect.GetPrimitives)
                    e.EndPrimitive();
            }

            return step < effect.DurationFrames;
        }
    }
}