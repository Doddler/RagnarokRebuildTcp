using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.FireBall)]
    public class FireBallHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendElementalHitEffect(attack.Src, attack.MotionTime, AttackElement.Fire, attack.HitCount);
        }
        
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Fire));
            target?.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }
        
        public override void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion(true);
        }
    }
}