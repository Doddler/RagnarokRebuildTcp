using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("Quagmire")]
    public class QuagmireEffect : IEffectHandler
    {
        private static float LastQuagSound;
        
        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Quagmire);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(999999);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step % 120 == 0)
            {
                var cam = CameraFollower.Instance;
                var id = cam.EffectIdLookup["Quagmire"];
                CameraFollower.Instance.CreateEffect(id, effect.transform.position, 0);

                if (Time.realtimeSinceStartup > LastQuagSound + 1f)
                {
                    AudioManager.Instance.OneShotSoundEffect(-1, "wizard_quagmire.ogg", effect.transform.position, 1f);
                    AudioManager.Instance.OneShotSoundEffect(-1, "wizard_quagmire.ogg", effect.transform.position, 1f);
                    LastQuagSound = Time.realtimeSinceStartup;
                }    
            }
            
            return effect.IsTimerActive;
        }
    }
}