using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.IncreaseAgility)]
    public class IncreaseAgiHandler : SkillHandlerBase
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
            {
                attack.Target.AttachFloatingTextIndicator("<font-weight=300><cspace=-0.5>AGI UP!", TextIndicatorType.Effect, -0.6f);
                AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_incagility.ogg", attack.Target.gameObject);
                AgiUpEffect.LaunchAgiUp(attack.Target);
            }
        }
    }
}