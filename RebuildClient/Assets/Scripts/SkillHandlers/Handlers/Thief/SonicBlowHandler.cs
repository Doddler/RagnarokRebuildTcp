using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.SonicBlow)]
    public class SonicBlowHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (src != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    src.SetAttackAnimationSpeed(0.2f);
                    src.Messages.SendAttackMotion(attack.Target, 0.2f, 0.2f + 0.2f * i, CharacterSkill.SonicBlow);
                }
                CameraFollower.Instance.AttachEffectToEntity("SonicBlow", src.gameObject, src.Id);
            }

            var angle = 0;
            var target = attack.Target;
            if (target != null && (attack.Result == AttackResult.NormalDamage || attack.Result == AttackResult.CriticalDamage))
            {
                var facing = target.SpriteAnimator.Direction;
                for (var i = 0; i < 8; i++)
                {
                    ////we want these to be sorted in front of the damage message (0.2s start time) so we use 0.19 and 0.192 respectively.
                    var hitType = attack.Result == AttackResult.CriticalDamage ? 2 : 1;
                    target.Messages.SendFaceDirection((FacingDirection)facing, attack.MotionTime + 0.19f * i);
                    target.Messages.SendHitEffect(src, attack.MotionTime + 0.192f * i, hitType);
                    if ((int)facing < 2)
                        facing += 8;
                    facing -= 2;
                }
            }
        }
    }
}