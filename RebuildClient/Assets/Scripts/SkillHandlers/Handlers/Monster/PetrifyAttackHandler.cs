using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Petrify, true)]
    public class PetrifyAttackHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target.Messages.SendHitEffect(attack.Src, attack.MotionTime, 2, attack.HitCount);
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //DefaultSkillCastEffect.Create(src);
            src?.PerformBasicAttackMotion();
            if (attack.Target != null && attack.Damage > 0)
            {
                CameraFollower.Instance.AttachEffectToEntity("StoneCurse", attack.Target.gameObject, src.Id);
                AudioManager.Instance.AttachSoundToEntity(src.Id, "_stonecurse.ogg", attack.Target.gameObject);
            }
        }
    }
}