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
            if (lockOnMaterial == null)
            {
                lockOnMaterial = new Material(ShaderCache.Instance.AlphaBlendParticleShader);
                lockOnMaterial.mainTexture = Resources.Load<Texture2D>("LockOn128");
            }

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.CastLockOn);
            effect.FollowTarget = followTarget;
            effect.UpdateOnlyOnFrameChange = false;
            effect.Duration = duration;

            var angle = 0f;
            for (var i = 0; i < 2; i++)
            {
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture3D, lockOnMaterial);
                var data = prim.GetPrimitiveData<Texture3DData>();
                prim.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                data.Size = new Vector2(30 / 5f, 30 / 5f);
                data.ScalingSpeed = new Vector2(-60f, -60f);
                data.MaxSize = data.Size;
                data.MinSize = new Vector2(20 / 5f, 20 / 5f);
                data.Flags = RoPrimitiveHandlerFlags.CycleColors;
                data.Color = new Color(250 / 255f, 150 / 255f, 150 / 255f, 1f);
                data.ColorChange = new Vector4(0f, 900f, 900f, 0f);
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

            return step < effect.DurationFrames;
        }
    }
}