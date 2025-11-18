using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills.Knight;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers.Knight
{
    [SkillHandler(CharacterSkill.SpearBoomerang)]
    public class SpearBoomerangHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //DefaultSkillCastEffect.Create(src);
            src.LookAtOrDefault(attack.Target);
            src.PerformThrowMotion();
            CameraFollower.Instance.AttachEffectToEntity("SpearBoomerangSelf", src.gameObject, src.Id);
            if(attack.Target != null)
                SpearBoomerangEffect.CreateSpearBoomerang(src, attack.Target.gameObject, attack.MotionTime);
            //AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject);
        }
    }
}