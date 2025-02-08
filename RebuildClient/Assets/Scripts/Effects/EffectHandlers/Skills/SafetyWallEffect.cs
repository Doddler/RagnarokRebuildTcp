using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("SafetyWall")]
    public class SafetyWallEffect : IEffectHandler
    {
        public static Ragnarok3dEffect LaunchSafetyWall(GameObject target)
        {
            // var obj = Resources.Load<GameObject>("TempSafetyWall");
            // var temp = GameObject.Instantiate(obj, target.transform);
            // temp.transform.localPosition = Vector3.zero;
            
            CameraFollower.Instance.AttachEffectToEntity("SafetyWall", target);
            
            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SafetyWall);
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.SafetyWall);
            effect.SetDurationByFrames(60 * 60);
            effect.FollowTarget = target;
            effect.DestroyOnTargetLost = false;
            effect.transform.position = target.transform.position;
            effect.transform.localScale = new Vector3(2, 2, 2);
            effect.Flags[0] = 0;
            
            AudioManager.Instance.OneShotSoundEffect(-1, "ef_glasswall.ogg", effect.transform.position, 0.8f);
            
            var prim = effect.LaunchPrimitive(PrimitiveType.Heal, mat, 60f);
            prim.CreateParts(4);

            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 3600,
                CoverAngle = 360,
                MaxHeight = 34,
                Angle = Random.Range(0f, 360f),
                Distance = 3.6f,
                RiseAngle = 90
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 3600,
                CoverAngle = 360,
                MaxHeight = 37,
                Angle = Random.Range(0f, 360f),
                Distance = 3.3f,
                RiseAngle = 90
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 3600,
                CoverAngle = 360,
                MaxHeight = 40,
                Angle = Random.Range(0f, 360f),
                Distance = 3f,
                RiseAngle = 90
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = false,
            };

            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                    prim.Parts[i].Flags[j] = 0; //0 is no spin, 1 is spin
                }
            }
            
            return effect;
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

            if (effect.FollowTarget == null)
            {
                effect.Flags[0] = 1;
                foreach (var p in effect.Primitives)
                {
                    for (var i = 0; i < p.PartsCount; i++)
                        p.Parts[i].AlphaTime = p.Step;
                }
            }

            return step < effect.DurationFrames;
        }
    }
}