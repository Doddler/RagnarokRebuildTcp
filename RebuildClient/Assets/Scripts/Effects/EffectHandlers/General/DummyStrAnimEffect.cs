using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    public class DummyStrAnimEffect : IEffectHandler
    {
        public static void Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.HammerFall);
            effect.SetDurationByFrames(60);
            effect.FollowTarget = target.gameObject;
            effect.DestroyOnTargetLost = true;

            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["HammerFall"];
            CameraFollower.Instance.AttachEffectToEntity(id, target.gameObject);
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