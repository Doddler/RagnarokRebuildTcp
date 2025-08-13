using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.ChargeArrow)]
    public class ChargeArrowHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target.Messages.SendHitEffect(attack.Src, attack.DamageTiming, 1, 1);
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            //AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject);

            if (attack.Target == null)
                return;
            
            //Debug.Break();
            //Time.timeScale = 0.1f;
            
            ArcherArrow.CreateArrow(src, attack.Target.gameObject, attack.MotionTime);
            //attack.Target.Messages.SendHitEffect(src, attack.MotionTime + arrow.Duration);
        }
    }
}