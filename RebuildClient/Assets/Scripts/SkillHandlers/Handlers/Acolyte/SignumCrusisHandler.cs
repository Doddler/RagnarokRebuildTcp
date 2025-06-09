using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.SignumCrusis)]
    public class SignumCrusisHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Holy));
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (src == null)
                return;
            
            src.PerformSkillMotion();
            AudioManager.Instance.OneShotSoundEffect(src.Id, "ef_signum.ogg", src.transform.position, 1f, 0.5f);
            AudioManager.Instance.OneShotSoundEffect(src.Id, "ef_bash.ogg", src.transform.position, 1f, 0.5f);
            CameraFollower.Instance.AttachEffectToEntity("SignumCrusis", src.gameObject, src.Id);
        }
    }
}