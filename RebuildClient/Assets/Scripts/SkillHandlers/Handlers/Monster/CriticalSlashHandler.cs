using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.CriticalAttack, true)]
    public class CriticalSlashHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target.Messages.SendHitEffect(attack.Src, attack.MotionTime, 2, attack.HitCount);
        }

        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject);
        }
    }
}