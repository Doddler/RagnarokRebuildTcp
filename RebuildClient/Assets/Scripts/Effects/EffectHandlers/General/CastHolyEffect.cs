using Assets.Scripts.Objects;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("CastHolyEffect")]
    public class CastHolyEffect : IEffectHandler
    {
        public static Material HolyCastMaterial;
        public static Material GhostCastMaterial;

        private static Material PickMaterialForElement(AttackElement element)
        {
            switch (element)
            {
                case AttackElement.Holy:
                    if (HolyCastMaterial == null)
                    {
                        HolyCastMaterial = new Material(ShaderCache.Instance.AdditiveShader)
                        {
                            mainTexture = Resources.Load<Texture2D>("ring_white"),
                            renderQueue = 3001
                        };
                    }
                    return HolyCastMaterial;
                case AttackElement.Ghost:
                default:
                    if (GhostCastMaterial == null)
                    {
                        GhostCastMaterial = new Material(ShaderCache.Instance.AdditiveShader)
                        {
                            mainTexture = Resources.Load<Texture2D>("ring_yellow"),
                            color = new Color(1f, 1f, 170f/255f),
                            renderQueue = 3001
                        };
                    }
                    return GhostCastMaterial;
            }
        }
        
        public static Ragnarok3dEffect BeginCasting6(float duration, GameObject followTarget, AttackElement element)
        {
            if (duration < 0.9333f)
                duration = 0.9333f;
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.CastHolyEffect);
            effect.SetDurationByTime(duration);
            effect.FollowTarget = followTarget;
            effect.transform.localScale = new Vector3(2, 2, 2);

            if (HolyCastMaterial == null && element == AttackElement.Holy)
            {
                HolyCastMaterial = new Material(ShaderCache.Instance.AdditiveShader)
                {
                    mainTexture = Resources.Load<Texture2D>("ring_white"),
                    renderQueue = 3001
                };
            }

            var mat = PickMaterialForElement(element);

            AudioManager.Instance.OneShotSoundEffect(effect.SourceEntityId, $"ef_beginspell.ogg", followTarget.transform.position);

            var prim = effect.LaunchPrimitive(PrimitiveType.Aura, mat, duration);
            prim.FrameDuration = Mathf.FloorToInt(duration * 60);

            prim.CreateParts(4);

            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 360,
                MaxHeight = 20,
                Angle = 180,
                Alpha = 45 + 135,
                AlphaTime = 0,
                Distance = 4.1f, //4.5f,
                RiseAngle = 80,
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 360,
                MaxHeight = 19,
                Angle = 270,
                Alpha = 45 + 90,
                AlphaTime = 0,
                Distance = 4.1f, //4.5f,
                RiseAngle = 80
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 360,
                MaxHeight = 18,
                Angle = 0,
                Alpha = 45 + 45,
                AlphaTime = 0,
                Distance = 4.1f, //4.5f,
                RiseAngle = 80
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 360,
                MaxHeight = 17,
                Angle = 90,
                Alpha = 45 + 0,
                AlphaTime = 0,
                Distance = 4.1f, //4.5f,
                RiseAngle = 80
            };

            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                    prim.Parts[i].Flags[j] = 0;
                }

                if (duration < 1f)
                {
                    prim.Parts[i].Alpha = 0;
                    prim.Parts[i].Step = -i * 5;
                }
            }

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            return pos < effect.Duration;
        }
    }
}