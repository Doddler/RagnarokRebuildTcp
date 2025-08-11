using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("BloodDrain")]
    public class BloodDrainEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable src, ServerControllable target, int projectileCount, Color color, float delay)
        {
            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.ParticleAlphaBlend);
            var sprite = EffectSharedMaterialManager.GetParticleSprite("particle1");
            mat.mainTexture = sprite.texture;
            projectileCount = Mathf.Clamp(projectileCount, 1, 5);
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.BloodDrain);
            effect.SetDurationByFrames(150);
            effect.SourceEntity = src;
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.localPosition = target.transform.position + new Vector3(0f, 2f, 0f);
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.Flags[0] = projectileCount;
            
            //calculate forward angle
            var pos1 = new Vector2(target.transform.position.x, target.transform.position.z);
            var pos2 = new Vector2(src.transform.position.x, src.transform.position.z);
            var angle = Vector2.SignedAngle((pos2 - pos1).normalized, Vector2.up);
            if (angle < 0)
                angle += 360f;
            
            var distance = Vector2.Distance(pos1, pos2); //target distance stored here
            var upAngle = SoulStrikeEffect.StepStart[projectileCount - 1]; //starting up angle
            var upStep = SoulStrikeEffect.StepSizes[projectileCount - 1];
            for (var i = 0; i < projectileCount; i++)
                LaunchProjectile(effect, sprite, mat, color, angle, upAngle + upStep * i, distance, delay);
            
            return effect;
        }
        
        private static void LaunchProjectile(Ragnarok3dEffect effect, Sprite sprite, Material mat, Color color, float forwardAngle, float upAngle, float distance, float delay)
        {
            var frames = 40f;
            var prim = effect.LaunchPrimitive(PrimitiveType.Particle3DSpline, mat, 1f);
            prim.CreateSegments(7);
            prim.DelayTime = delay;
            prim.FrameDuration = Mathf.RoundToInt(frames); //shorter than proper duration to allow for the segments to fade out

            var backSpeed = 0.4f;

            var data = prim.GetPrimitiveData<Particle3DSplineData>();
            data.Position = Vector3.zero;
            data.Velocity = new Vector2(distance / frames - backSpeed, 0.4f);
            data.Size = 1.7f;
            data.Acceleration = new Vector2(-(-backSpeed / frames) * 2, -(data.Velocity.y / frames) * 2f);
            data.Sprite = sprite;
            data.Rotation = Quaternion.Euler(0, forwardAngle, upAngle);
            data.Color = color;
        }
    }
}