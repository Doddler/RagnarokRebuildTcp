using System.Collections.Generic;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("HealEffect")]
    public class HealEffect : IEffectHandler
    {
        private static readonly Dictionary<string, Material> HealMaterials = new();
        private static readonly Dictionary<string, GameObject> ParticlePrefab = new();

        private static (Material, GameObject) GetResources(string texture, string prefabName)
        {
            if (!HealMaterials.TryGetValue(texture, out var mat))
            {
                mat = new Material(ShaderCache.Instance.AdditiveShader);

                mat.mainTexture = Resources.Load<Texture2D>(texture);
                mat.renderQueue = 3001;
                HealMaterials.Add(texture, mat);
            }

            if (!ParticlePrefab.TryGetValue(prefabName, out var prefab))
            {
                prefab = Resources.Load<GameObject>(prefabName);
                ParticlePrefab.Add(prefabName, prefab);
            }

            return (mat, prefab);
        }

        public static Ragnarok3dEffect CreateAutoLevel(GameObject followTarget, int healAmount)
        {
            switch (healAmount)
            {
                case < 200:
                    return HealEffect.Create(followTarget, 0);
                case < 2000:
                    return HealEffect.Create(followTarget, 1);
                default:
                    return HealEffect.Create(followTarget, 2);
            }
        }

        public static Ragnarok3dEffect Create(GameObject followTarget, int healStrength = 1)
        {
            //default heal strength 1
            var healLength = 1.6f;
            var partCount = 2;
            var part0Height = 40 + Random.Range(0, 20);
            var part0Size = 4.6f;
            var part1Height = 40f;
            var part1Size = 4.8f;
            var alphaRampUpTime = 60f; //30 frames @ 60fps
            var alphaRampUpSpeed = 4f; //per frame @ 60fps
            var texture = "alpha_down";
            var particlePrefab = "HealParticles1";
            var color = new Color32(140, 210, 140, 255);

            if (healStrength == 0)
            {
                part0Height = 40 + Random.Range(0, 20);
                part0Size = 4.5f + (Random.Range(0, 100) * 0.005f);
                part1Height = part0Height;
                part1Size = part0Size + 0.2f;
                alphaRampUpTime = 30f;
                alphaRampUpSpeed = 2f;
                particlePrefab = "HealParticles0";
                color = new Color32(210, 210, 210, 255);
            }

            if (healStrength == 2)
            {
                part0Height = 60;
                part1Height = 60;
                part0Size = 5f;
                texture = "ring_white";
                partCount = 4;
            }

            var (mat, prefab) = GetResources(texture, particlePrefab);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.CastEffect);
            effect.SetDurationByTime(healLength + 0.4f);
            effect.FollowTarget = followTarget;
            effect.transform.localScale = new Vector3(2f, 2f, 2f);

            AudioManager.Instance.OneShotSoundEffect(effect.SourceEntityId, $"_heal_effect.ogg", followTarget.transform.position);

            var prim = effect.LaunchPrimitive(PrimitiveType.Heal, mat, 1.6f);
            prim.CreateParts(partCount);
            prim.Flags[0] = healStrength;

            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = alphaRampUpSpeed,
                AlphaTime = alphaRampUpTime,
                CoverAngle = 360,
                MaxHeight = part0Height,
                Angle = Random.Range(0f, 360f),
                Distance = part0Size,
                RiseAngle = 90,
                Color = color
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = alphaRampUpSpeed,
                AlphaTime = alphaRampUpTime,
                CoverAngle = 360,
                MaxHeight = part1Height,
                Angle = Random.Range(0f, 360f),
                Distance = part1Size, //4f,
                RiseAngle = 90,
                RotStart = 180,
                Color = color
            };

            if (partCount > 2)
            {
                prim.Parts[2] = new EffectPart()
                {
                    Active = true,
                    Step = 0,
                    AlphaRate = alphaRampUpSpeed,
                    AlphaTime = alphaRampUpTime,
                    CoverAngle = 360,
                    MaxHeight = 12,
                    Angle = Random.Range(0f, 360f),
                    Distance = 5,
                    RiseAngle = 50,
                    Color = color
                };
            }

            if (partCount > 3)
            {
                prim.Parts[3] = new EffectPart()
                {
                    Active = true,
                    Step = 0,
                    AlphaRate = alphaRampUpSpeed,
                    AlphaTime = alphaRampUpTime,
                    CoverAngle = 360,
                    MaxHeight = 12,
                    Angle = Random.Range(0f, 360f),
                    Distance = 5.2f,
                    RiseAngle = 48,
                    Color = color
                };
            }

            for (var i = 0; i < partCount; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                    prim.Parts[i].Flags[j] = 0;
                }
            }

            var particles = GameObject.Instantiate(prefab);
            particles.transform.parent = effect.gameObject.transform;
            particles.transform.localPosition = Vector3.zero;
            effect.AimTarget = particles;
            // particles.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            return effect;
        }

        public void SceneChangeResourceCleanup()
        {
            ParticlePrefab.Clear();
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(effect.CurrentPos >= effect.Duration && effect.AimTarget != null)
                GameObject.Destroy(effect.AimTarget); //particles are stored here for lack of a better spot
            
            //nothing to do but wait for it to end
            return effect.CurrentPos < effect.Duration;
        }
    }
}