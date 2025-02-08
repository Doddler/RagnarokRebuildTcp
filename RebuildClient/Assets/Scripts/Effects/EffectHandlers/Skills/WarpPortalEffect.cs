using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("WarpPortal")]
    public class WarpPortalEffect : IEffectHandler
    {
        private static Material PortalMat;
        private static Material PillarMat;
        private static Material WindMat;

        public static void StartWarpPortal(GameObject target)
        {
            // var opening = Resources.Load<GameObject>("TempWarpPortal");
            // var go = GameObject.Instantiate(opening, target.transform);
            // go.transform.localPosition = Vector3.zero;

            if (PortalMat == null)
            {
                PortalMat = new Material(ShaderCache.Instance.AdditiveShader)
                {
                    mainTexture = Resources.Load<Texture2D>("ring_blue"),
                    color = new Color(100 / 255f, 100 / 255f, 255 / 255f),
                    renderQueue = 2999
                };

                PillarMat = new Material(ShaderCache.Instance.AdditiveShader)
                {
                    mainTexture = Resources.Load<Texture2D>("magic_violet"),
                    color = new Color(100 / 255f, 100 / 255f, 255 / 255f),
                    renderQueue = 3001
                };

                WindMat = new Material(ShaderCache.Instance.AdditiveShader)
                {
                    mainTexture = Resources.Load<Texture2D>("cloud11"),
                    color = new Color(100 / 255f, 100 / 255f, 255 / 255f),
                    renderQueue = 3001
                };
            }

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.WarpPortal);
            effect.SetDurationByTime(30f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.FollowTarget = target;
            effect.transform.position = target.transform.position;
            effect.transform.localScale = new Vector3(2, 2, 2);
        }

        public void SceneChangeResourceCleanup()
        {
            if (PortalMat != null) Object.Destroy(PortalMat);
            if (PillarMat != null) Object.Destroy(PillarMat);
            if (WindMat != null) Object.Destroy(WindMat);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.Flags[0] > 0)
            {
                foreach (var p in effect.Primitives)
                    if (p.IsActive)
                        return step < effect.DurationFrames;

                return false;
            }

            if (step == 0)
            {
                AudioManager.Instance.OneShotSoundEffect(-1, "ef_portal.ogg", effect.transform.position, 0.8f);
                LaunchPortalPillarEffect(effect);
            }

            if (step == 1)
                LaunchGroundPortalEffect(effect);

            if (step == 2)
                LaunchPortalWindEffect(effect);

            if (effect.FollowTarget == null)
            {
                effect.Flags[0] = 1;
                foreach (var p in effect.Primitives)
                {
                    if (p.PrimitiveType == PrimitiveType.WarpPortal)
                        p.Parts[3].Step = 9999;
                    else
                    {
                        for (var i = 0; i < p.PartsCount; i++)
                            p.Parts[i].AlphaTime = p.Step;
                    }
                }
            }

            return step < effect.DurationFrames;
        }

        private void LaunchPortalPillarEffect(Ragnarok3dEffect effect)
        {
            var prim = effect.LaunchPrimitive(PrimitiveType.Heal, PillarMat, 25f);
            prim.CreateParts(4);
            //prim.Flags[0] = healStrength;

            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 1400,
                CoverAngle = 360,
                MaxHeight = 50,
                Angle = Random.Range(0f, 360f),
                Distance = 4,
                RiseAngle = 90
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 1400,
                CoverAngle = 360,
                MaxHeight = 50,
                Angle = Random.Range(0f, 360f),
                Distance = 3,
                RiseAngle = 90
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 1400,
                CoverAngle = 360,
                MaxHeight = 50,
                Angle = Random.Range(0f, 360f),
                Distance = 2,
                RiseAngle = 90
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = false,
                Step = 0,
            };

            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                    prim.Parts[i].Flags[j] = 1;
                }
            }
        }

        private void LaunchGroundPortalEffect(Ragnarok3dEffect effect)
        {
            var prim = effect.LaunchPrimitive(PrimitiveType.WarpPortal, PortalMat, 25f);
            prim.FrameDuration = Mathf.FloorToInt(25 * 60);

            prim.CreateParts(4);

            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 360,
                MaxHeight = 6.001f,
                Angle = 0,
                Alpha = 0,
                AlphaTime = 100,
                Distance = 0,
                RiseAngle = 2,
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = -10,
                CoverAngle = 360,
                MaxHeight = 6.001f,
                Angle = 25,
                Alpha = 0,
                AlphaTime = 100,
                Distance = 0,
                RiseAngle = 3,
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = -20,
                CoverAngle = 360,
                MaxHeight = 6.001f,
                Angle = 50,
                Alpha = 0,
                AlphaTime = 100,
                Distance = 0,
                RiseAngle = 4,
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = false,
                Step = 0,
                AlphaTime = 1
            };

            for (var i = 0; i < prim.Parts.Length; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                    prim.Parts[i].Flags[j] = 1;
                }
            }
        }


        private void LaunchPortalWindEffect(Ragnarok3dEffect effect)
        {
            var prim = effect.LaunchPrimitive(PrimitiveType.Wind, WindMat, 25f);
            prim.CreateParts(4);
            //prim.Flags[0] = healStrength;

            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 0,
                AlphaTime = 1400,
                CoverAngle = 30,
                MaxHeight = 1.5f,
                Angle = 0,
                Distance = 9,
                RiseAngle = 90
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 0,
                AlphaTime = 1400,
                CoverAngle = 30,
                MaxHeight = 7.5f,
                Angle = 90,
                Distance = 7.5f,
                RiseAngle = 90
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 0,
                AlphaTime = 1400,
                CoverAngle = 30,
                MaxHeight = 6.5f,
                Angle = 180,
                Distance = 6,
                RiseAngle = 90
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 0,
                AlphaTime = 1400,
                CoverAngle = 30,
                MaxHeight = 7.5f,
                Angle = 270,
                Distance = 4.5f,
                RiseAngle = 90
            };

            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 1.5f;
                    prim.Parts[i].Flags[j] = 0;
                }
            }
        }
    }
}