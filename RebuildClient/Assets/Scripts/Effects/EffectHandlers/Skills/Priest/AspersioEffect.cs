using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Priest
{
    [RoEffect("Aspersio")]
    public class AspersioEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Aspersio);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(200);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;

            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["Aspersio"];
            CameraFollower.Instance.AttachEffectToEntity(id, target.gameObject);
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(step == 70)
                AudioManager.Instance.OneShotSoundEffect(-1, "priest_aspersio.ogg", effect.transform.position, 1f);

            if (step % 5 == 0 && step > 60 && step < 140)
            {
                
                var particleMat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.ParticleAlphaBlend);
                var prim = effect.LaunchPrimitive(PrimitiveType.ParticleOrbit, particleMat, 0.667f);
                prim.CreateParts(1);

                var dat = prim.GetPrimitiveData<ParticleOrbitData>();
                dat.Sprite = EffectSharedMaterialManager.GetParticleSprite("particle1");

                dat.Radius = 1.5f;
                dat.GravitySpeed = -0.19f * 18f; // 60 / 5 * 1.5
                dat.GravityAccel = -0.01f * 18f;
                // dat.RotationSpeed = 3f * 60f;
                // dat.RotationAccel = 12f * 60f;
                dat.Rotation = 0;
                dat.Size = 1.2f;
                dat.FadeOutTime = 0.5f;
                
                var angle = Random.Range(0, 360f);
                var dist = Random.Range(0, 1.5f) / 5f;
                var particlePos = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 6, Mathf.Cos(angle * Mathf.Deg2Rad) * dist);

                //var particlePos = new Vector3(0f, 0f, 7f / 5f);
                for (var i = 0; i < 1; i++)
                {
                    prim.Parts[i] = new EffectPart()
                    {
                        Active = true,
                        Step = 0,
                        Alpha = 255f,
                        Angle = i * 90,
                        Position = particlePos,
                        Color = Color.white,
                    };
                }
            }
            
            return effect.IsTimerActive;
        }
    }
}