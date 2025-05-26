using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.FireBolt, true)]
    public class FireBoltHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            //effect is handled inside firearrow effect
            //target.Messages.SendElementalHitEffect(attack.Src, attack.MotionTime, AttackElement.Fire, attack.HitCount);
        }
       
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Fire));
            
            if(target != null)
                target.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void InterruptSkillCasting(ServerControllable src)
        {
            src.EndEffectOfType(EffectType.CastEffect);
        }

        public override void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion(true);
            if(attack.Target != null)
                FireArrow.Create(src, attack.Target, attack.SkillLevel); //don't attach to the entity so the effect stays if they get removed
        }
    }
}


