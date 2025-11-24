using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills.Knight;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers.Knight
{
    [SkillHandler(CharacterSkill.SpearStab)]
    public class SpearStabHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendHitEffect(attack.Src, attack.DamageTiming, (int)HitEffectType.Pierce);
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.LookAtOrDefault(attack.Target);
            src.PerformBasicAttackMotion();
            CameraFollower.Instance.AttachEffectToEntity("SpearStab", src.gameObject, src.Id);
        }
    }
}