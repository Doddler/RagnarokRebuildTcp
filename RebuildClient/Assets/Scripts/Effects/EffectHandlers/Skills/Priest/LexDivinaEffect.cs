using Assets.Scripts.Network;
using Assets.Scripts.Objects;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Priest
{
    [RoEffect("LexDivina")]
    public class LexDivinaEffect : IEffectHandler
    {
        public static void Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.LexDivina);
            effect.SetDurationByFrames(90);
            effect.FollowTarget = target.gameObject;
            effect.DestroyOnTargetLost = true;

            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["LexDivina"];
            CameraFollower.Instance.AttachEffectToEntity(id, target.gameObject);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(step == 0 || step == 20 || step == 35 || step == 60 || step == 70)
                AudioManager.Instance.OneShotSoundEffect(-1, "priest_lexdivina.ogg", effect.transform.position, 0.7f);

            return step < effect.DurationFrames;
        }
    }
}