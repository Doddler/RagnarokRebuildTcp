using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.ComboAttack)]
    public class ComboAttackHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            if(attack.Damage > 0)
                attack.Target?.Messages.SendHitEffect(attack.Src, attack.MotionTime, 1, attack.HitCount);
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformBasicAttackMotion();
            if (attack.Target != null)
            {
                var attackName = attack.HitCount switch
                {
                    1 => "ComboAttack1",
                    2 => "ComboAttack2",
                    3 => "ComboAttack3",
                    4 => "ComboAttack4",
                    _ => "ComboAttack5"
                };

                CameraFollower.Instance.AttachEffectToEntity(attackName, attack.Target.gameObject);
            }
        }
    }
}