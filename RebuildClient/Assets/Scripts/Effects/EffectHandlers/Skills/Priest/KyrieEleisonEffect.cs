using Assets.Scripts.Network;
using Assets.Scripts.Objects;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Priest
{
    [RoEffect("KyrieEleison")]
    public class KyrieEleisonEffect : IEffectHandler
    {
        public static void Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.KyrieEleison);
            effect.SetDurationByFrames(60);
            effect.FollowTarget = target.gameObject;
            effect.DestroyOnTargetLost = true;
            effect.UpdateOnlyOnFrameChange = true;

            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["KyrieEleison"];
            CameraFollower.Instance.AttachEffectToEntity(id, target.gameObject);
            
            AudioManager.Instance.OneShotSoundEffect(target.Id, "priest_kyrie_eleison_b.ogg", effect.transform.position, 1f);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(step == 15)
                AudioManager.Instance.OneShotSoundEffect(-1, "priest_kyrie_eleison_a.ogg", effect.transform.position, 1f);

            return step < effect.DurationFrames;
        }
    }
}