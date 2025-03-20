using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Cure)]
    public class CureHandler : SkillHandlerBase
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
            if(attack.Target != null)
                CameraFollower.Instance.AttachEffectToEntity("Cure", attack.Target.gameObject, src.Id);
        }
    }
}