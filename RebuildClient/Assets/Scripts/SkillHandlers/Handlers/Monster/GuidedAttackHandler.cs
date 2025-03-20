using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.GuidedAttack)]
    public class GuidedAttackHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            if (attack.Damage > 0 && attack.Target != null)
            {
                attack.Target.Messages.SendHitEffect(attack.Src, attack.MotionTime, 1, attack.HitCount);
                CameraFollower.Instance.AttachEffectToEntity("GuidedAttack", attack.Target.gameObject, attack.Target.Id);
            }
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformBasicAttackMotion();
        }
    }
}