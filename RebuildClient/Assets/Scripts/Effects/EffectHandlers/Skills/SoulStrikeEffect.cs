using System.Data;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("SoulStrike")]
    public class SoulStrikeEffect : IEffectHandler
    {
        private static Material ghostMaterial;
        private static Material darkMaterial;


        private static Material PickMaterialForElement(bool isDark)
        {
            if (!isDark)
            {
                if (ghostMaterial == null)
                {
                    ghostMaterial = new Material(ShaderCache.Instance.InvAlphaShader)
                    {
                        renderQueue = 3001
                    };
                }

                return ghostMaterial;
            }

            if (darkMaterial == null)
            {
                darkMaterial = new Material(ShaderCache.Instance.InvAlphaShader)
                {
                    renderQueue = 3001
                };
            }

            return darkMaterial;
        }

        public static readonly int[] StepSizes = {0, 140, 80, 60, 45};
        public static readonly int[] StepStart = {0, -70, -80, -90, -90};

        public static Ragnarok3dEffect LaunchEffect(ServerControllable caster, GameObject target, int projectileCount, bool isDark)
        {
            var mat = PickMaterialForElement(isDark);
            var sprite = EffectParticleManager.Instance.Sprites[isDark ? 4 : 0];
            mat.mainTexture = sprite.texture;
            projectileCount = Mathf.Clamp(projectileCount, 1, 5);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.SoulStrike);
            effect.SetDurationByFrames(150);
            effect.SourceEntity = caster;
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.localPosition = caster.transform.position + new Vector3(0f, 2f, 0f);
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.Flags[0] = projectileCount;

            //calculate forward angle
            var pos1 = new Vector2(caster.transform.position.x, caster.transform.position.z);
            var pos2 = new Vector2(target.transform.position.x, target.transform.position.z);
            var angle = Vector2.SignedAngle((pos2 - pos1).normalized, Vector2.up);
            if (angle < 0)
                angle += 360f;

            var distance = Vector2.Distance(pos1, pos2); //target distance stored here
            var upAngle = StepStart[projectileCount - 1]; //starting up angle
            var upStep = StepSizes[projectileCount - 1];
            for (var i = 0; i < projectileCount; i++)
                LaunchSoulStrike(effect, sprite, mat, angle, upAngle + upStep * i, distance, (5 + i * 10) / 60f); //frame 5, every 10 frames after

            return effect;
        }

        private static void LaunchSoulStrike(Ragnarok3dEffect effect, Sprite sprite, Material mat, float forwardAngle, float upAngle, float distance, float delay)
        {
            var frames = 40f;
            var prim = effect.LaunchPrimitive(PrimitiveType.Particle3DSpline, mat, 1f);
            prim.CreateSegments(12);
            prim.DelayTime = delay;
            prim.FrameDuration = Mathf.RoundToInt(frames); //shorter than proper duration to allow for the segments to fade out

            var backSpeed = 0.7f;

            var data = prim.GetPrimitiveData<Particle3DSplineData>();
            data.Position = Vector3.zero;
            data.Velocity = new Vector2(distance / frames - backSpeed, 0.4f);
            data.Size = 2.5f;
            data.Acceleration = new Vector2(-(-backSpeed / frames) * 2, -(data.Velocity.y / frames) * 2f);
            data.Sprite = sprite;
            data.Rotation = Quaternion.Euler(0, forwardAngle, upAngle);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.Flags[1] >= effect.Flags[0] || (step + 5) % 11 != 0)
                return step <= effect.DurationFrames;

            if (effect.SourceEntity != null)
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntityId, $"ef_soulstrike.ogg", effect.SourceEntity.transform.position);

            effect.Flags[1]++;
            
            return true;
        }

        public void SceneChangeResourceCleanup()
        {
            if (ghostMaterial != null)
                Object.Destroy(ghostMaterial);
            if (darkMaterial != null)
                Object.Destroy(darkMaterial);
        }
    }
}