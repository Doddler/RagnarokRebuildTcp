using Assets.Scripts.Network;
using Assets.Scripts.Objects;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Priest
{
    [RoEffect("LexAeterna")]
    public class LexAeternaEffect : IEffectHandler
    {
        public static void Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.LexAeterna);
            effect.SetDurationByFrames(60);
            effect.FollowTarget = target.gameObject;
            effect.DestroyOnTargetLost = true;

            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["LexAeterna"];
            CameraFollower.Instance.AttachEffectToEntity(id, target.gameObject);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(step == 15)
                AudioManager.Instance.OneShotSoundEffect(-1, "priest_lexaeterna.ogg", effect.transform.position, 1f);

            return step < effect.DurationFrames;
        }
    }
}