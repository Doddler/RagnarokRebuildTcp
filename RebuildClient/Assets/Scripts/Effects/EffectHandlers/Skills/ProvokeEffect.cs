using Assets.Scripts.Network;
using Assets.Scripts.Objects;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("Provoke")]
    public class ProvokeEffect : IEffectHandler
    {
        public static void Provoke(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Provoke);
            effect.SetDurationByFrames(35);
            effect.FollowTarget = target.gameObject;

            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["Provoke"];
            CameraFollower.Instance.AttachEffectToEntity(id, target.gameObject, target.Id);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(step == 5 || step == 30)
                AudioManager.Instance.OneShotSoundEffect(-1, "swordman_provoke.ogg", effect.transform.position, 0.7f);

            return step < effect.DurationFrames;
        }
    }
}