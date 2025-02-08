using Assets.Scripts.Objects;
using Assets.Scripts.UI.ConfigWindow;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("WarpPortalOpening")]
    public class WarpPortalOpeningEffect : IEffectHandler
    {
        public static Material WarpPortalOpenMat;
        
        public static Ragnarok3dEffect StartWarpPortalOpen(GameObject target)
        {
            // var opening = Resources.Load<GameObject>("TempWarpPortalOpening");
            // var go = GameObject.Instantiate(opening, target.transform);
            // go.transform.localPosition = Vector3.zero;
            //
            if (WarpPortalOpenMat == null)
            {
                WarpPortalOpenMat = new Material(ShaderCache.Instance.AdditiveShader)
                {
                    mainTexture = Resources.Load<Texture2D>("ring_blue"),
                    renderQueue = 2999
                };
            }
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.WarpPortalOpening);
            effect.SetDurationByFrames(600);
            effect.FollowTarget = target;
            effect.transform.position = target.transform.position;
            effect.transform.localScale = new Vector3(2, 2, 2);
            effect.Flags[0] = 0;
            
            var prim = effect.LaunchPrimitive(PrimitiveType.WarpPortal, WarpPortalOpenMat, 5f);
            prim.FrameDuration = Mathf.FloorToInt(3 * 60);

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

            return effect;
        }
        
        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.Primitives.Count == 0)
                return false;
            
            if(effect.Flags[0] > 0)
                return step < effect.DurationFrames; 
            
            if(step % 14 == 0)
                AudioManager.Instance.OneShotSoundEffect(-1, "ef_readyportal.ogg", effect.transform.position, 0.8f);
            
            //if our object has vanished we will stop looping the portal effect 
            if (effect.FollowTarget == null)
            {
                effect.Primitives[0].Parts[3].Step = 9999;
                effect.Flags[0] = 1;
            }

            return step < effect.DurationFrames;
        }

        public void SceneChangeResourceCleanup()
        {
            GameObject.Destroy(WarpPortalOpenMat);
            WarpPortalOpenMat = null;
        }
    }
}