using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills.Knight;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers.Knight
{
    [SkillHandler(CharacterSkill.BowlingBash)]
    public class BowlingBashHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldAttackMotionForCast(src, castTime - 0.1f, 0.1f);
            //src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Water));
            
            if(target != null)
                target.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformBasicAttackMotion();
            
            var id = CameraFollower.Instance.EffectIdLookup["BowlingBash"];
            CameraFollower.Instance.AttachEffectToEntity(id, src.gameObject, src.Id);

            if(attack.Target != null)
                BowlingBashImpactEffect.Create(attack.Target, attack.DamageTiming);
        }
    }
}