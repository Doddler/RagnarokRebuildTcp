using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Mammonite)]
    public class MammoniteHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            //attack.Target.Messages.SendHitEffect(attack.Src, attack.MotionTime, 1, attack.HitCount);
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject, 1.2f);

            if (attack.Target == null)
                return;
            
            CameraFollower.Instance.AttachEffectToEntity("Mammonite", attack.Target.gameObject, src.Id);
            CameraFollower.Instance.AttachEffectToEntity("MammoniteCoins", attack.Target.gameObject, src.Id);
        }
    }
}