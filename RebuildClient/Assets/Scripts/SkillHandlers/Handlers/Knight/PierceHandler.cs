using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers.Knight
{
    [SkillHandler(CharacterSkill.Pierce)]
    public class PierceHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendHitEffect(attack.Src, attack.MotionTime, 3, 1);
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            CameraFollower.Instance.AttachEffectToEntity("PierceSelf", src.gameObject, src.Id);
            //AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject);
        }
    }
}