using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills.Priest;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.ImpositioManus)]
    public class ImpositioHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Holy));
            
            if(target != null)
                target.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();
            if (attack.Target != null)
                ImpositioEffect.Create(attack.Target);
        }
    }
}