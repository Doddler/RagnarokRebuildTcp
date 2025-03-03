using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Silence)]
    public class SilenceAttackHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target.Messages.SendHitEffect(attack.Src, attack.MotionTime, 2, attack.HitCount);
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            if(attack.Target != null)
                CameraFollower.Instance.AttachEffectToEntity("SilenceAttack", attack.Target.gameObject, attack.Target.Id);
        }
    }
}