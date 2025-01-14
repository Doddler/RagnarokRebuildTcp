using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("StealEffect")]
    public class StealEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Steal(ServerControllable src, ServerControllable target)
        {
            if (target == null)
            {
                AudioManager.Instance.OneShotSoundEffect(src.Id, $"ef_steal.ogg", src.transform.position);
                return null;
            }

            AudioManager.Instance.OneShotSoundEffect(target.Id, $"ef_steal.ogg", target.transform.position);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.StealEffect);

            effect.transform.localScale = Vector3.one;
            effect.SourceEntity = target;
            effect.SetDurationByFrames(60);
            effect.FollowTarget = target.gameObject;
            effect.UpdateOnlyOnFrameChange = true;
            effect.SetSortingGroup("Default", 0); //appear in front of damage indicators
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0)
            {
                for (var i = 0; i < 10; i++)
                {
                    var duration = 0.6f;
                    var dir = Quaternion.Euler(0, Random.Range(0, 360), Random.Range(-50, 50)) * Vector3.forward;
                    var velocity = Random.Range(0.5f, 1f) * 20f;
                    var gravity = Random.Range(0.6f, 1.5f) * 20f;
                    var particle = new EffectParticle()
                    {
                        Lifetime = duration,
                        FadeStartTime = duration - (duration / 3f),
                        Size = Random.Range(0.2f, 0.4f),
                        AlphaSpeed = 999f,
                        AlphaMax = 1f,
                        Color = new Color32(255, 255, 255, 255),
                        Position = new Vector3(0, Random.Range(0.4f, 0.8f), 0f),
                        Velocity = dir * velocity,
                        Acceleration = -(velocity / duration) / 2f,
                        Gravity = gravity,
                        GravityAccel = -(gravity / duration) / 2f,
                        ParticleId = 6, //yellow star
                        Mode = ParticleDisplayMode.Normal,
                        RelativeTarget = effect.gameObject //we want the particles to move with the player
                    };
                    EffectParticleManager.Instance.AddParticle(ref particle);
                }
            }

            return effect.CurrentPos < effect.Duration;
        }
    }
}