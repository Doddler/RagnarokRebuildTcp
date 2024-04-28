using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("CastEffect")]
    public class CastEffect : IEffectHandler
    {
        private static readonly Dictionary<string, Material> CastMaterials = new();
        
        public static Ragnarok3dEffect Create(float duration, string texture, GameObject followTarget, bool useInvAlphaShader = false)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.CastEffect);
            effect.Duration = duration;
            effect.FollowTarget = followTarget;
            
            if (!CastMaterials.TryGetValue(texture, out var mat))
            {
                if(!useInvAlphaShader)
                    mat = new Material(ShaderCache.Instance.AdditiveShader);
                else
                    mat = new Material(ShaderCache.Instance.InvAlphaShader);
                mat.mainTexture = Resources.Load<Texture2D>(texture);
                mat.renderQueue = 3001;
                CastMaterials.Add(texture, mat);
            }

            effect.transform.localScale = new Vector3(2f, 2f, 2f);
            
            var prim = effect.LaunchPrimitive(PrimitiveType.Cylender, mat, duration);

            prim.CreateParts(4);
            
            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 315,
                MaxHeight = 25,
                Angle = 0,
                Alpha = 180,
                Distance = 4.5f, //4.5f,
                RiseAngle = 70
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 315,
                MaxHeight = 22,
                Angle = 90,
                Alpha = 180,
                Distance = 4.5f, //5f,
                RiseAngle = 57
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 315,
                MaxHeight = 19,
                Angle = 45,
                Alpha = 180,
                Distance = 4.5f, //5.5f,
                RiseAngle = 45
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = true,
                Step = 0,
                CoverAngle = 360,
                MaxHeight = 250,
                Angle = Random.Range(0f, 360f),
                Alpha = 70,
                Distance = 4f, //4f,
                RiseAngle = 89
            };

            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                    prim.Parts[i].Flags[j] = 0;
                }
            }
            
            return effect;
        }
        
        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            //nothing to do but wait for it to end
            return effect.CurrentPos < effect.Duration;
        }

    }
}