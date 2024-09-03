using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("HammerFall")]
    public class HammerFallEffect : IEffectHandler
    {
        public static void CreateHammerFall(Vector3 target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.HammerFall);
            effect.SetDurationByFrames(60);
            effect.transform.position = target;

            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["HammerFall"];
            CameraFollower.Instance.CreateEffect(id, target, 0);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(step == 30)
                AudioManager.Instance.OneShotSoundEffect(-1, "wizard_fire_pillar_b.ogg", effect.transform.position, 0.7f);

            if (step == 35)
                CameraFollower.Instance.ShakeTime = 0.3f;
                

            return step < effect.DurationFrames;
        }
    }
}