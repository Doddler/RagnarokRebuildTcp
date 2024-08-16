using System.Collections.Generic;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("HitEffect")]
    public class HitEffect : IEffectHandler
    {
        private static readonly Dictionary<string, Material> materials = new();
        
        public static Ragnarok3dEffect Hit1(Vector3 src, Vector3 target)
        {
            //generate hit particles
            var dir = (src - target).normalized;
            for (var i = 0; i < 4; i++)
            {
                var pVelocity = Quaternion.Euler(0, Random.Range(-30, 30), Random.Range(-50, 50)) * dir;
                // if (pVelocity.y < 0)
                //     pVelocity.y = -pVelocity.y;
                pVelocity *= Random.Range(6f, 15f) * 1.5f;
                var gravity = 0f;
                if (i >= 2)
                {
                    gravity = Random.Range(10f, 40f);
                    pVelocity = -pVelocity;
                }
                        
                var duration = 0.2f + Random.Range(0, 0.3f);
                EffectParticleManager.Instance.AddParticle(Random.Range(6f, 16f)/5f, target, pVelocity, 
                    duration, new Color32(255, 255, 255, 20), -pVelocity.magnitude / (1 / duration) / 2f, gravity);
            }
            
            //generate ring effect
            if (!materials.TryGetValue("ring_blue", out var mat))
            {
                mat = new Material(ShaderCache.Instance.PerspectiveAlphaShader);
                mat.mainTexture = Resources.Load<Texture2D>("ring_blue");
                mat.renderQueue = 3001;
                materials.Add("ring_blue", mat);
            }
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.HitEffect);
            effect.SetDurationByFrames(9);
            
            // Debug.Log(effect.Duration);
           
            var prim = effect.LaunchPrimitive(PrimitiveType.Cylinder3D, mat, effect.Duration);
            prim.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            prim.transform.position = target + dir / 10f + Vector3.up * 0.5f;
            prim.transform.localScale = Vector3.one * 0.2f;
            
            var data = prim.GetPrimitiveData<CylinderData>();

            var speed = (0.7f * 60) / 5f;
            
            data.Velocity = dir * speed;
            data.Acceleration = -(speed / effect.Duration) / 2f;
            data.Height = 3.5f;
            data.InnerRadius = 5f;
            data.OuterRadius = 10f;
            data.Height = 3.5f;
            data.Alpha = 255;
            data.MaxAlpha = 255;
            data.FadeOutLength = effect.Duration / 2f;

            return null;
        }


        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            return step < effect.DurationFrames;
        }
    }
}