using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("LordOfVermilion")]
    public class LordOfVermilionEffect : IEffectHandler
    {
        public static void LaunchEffect(Vector3 target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.LordOfVermilion);
            effect.SetDurationByFrames(210);
            effect.transform.position = target;

            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["LoV"];
            CameraFollower.Instance.CreateEffect(id, target, 0);
        }
        
        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            switch (step)
            {
                case 0:
                    AudioManager.Instance.OneShotSoundEffect(-1, "wizard_meteo.ogg", effect.transform.position, 0.7f);
                    AudioManager.Instance.OneShotSoundEffect(-1, "hunter_blastmine.ogg", effect.transform.position, 0.7f);
                    break;
                case 10:
                    AudioManager.Instance.OneShotSoundEffect(-1, "hunter_blastmine.ogg", effect.transform.position, 0.7f);
                    break;
                case 20:
                case 50:
                case 80:
                case 100:
                case 130:
                case 140:
                case 180:
                case 200:
                    AudioManager.Instance.OneShotSoundEffect(-1, "wizard_meteo.ogg", effect.transform.position, 0.7f);
                    break;
            }
            
            if (step == 50)
                CameraFollower.Instance.ShakeTime = 2.8f;
                

            return step < effect.DurationFrames;
        }
    }
}