using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.DecreaseAgility)]
    public class DecreaseAgiHandler: SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Holy));
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (attack.Target != null && attack.Result != AttackResult.Miss)
            {
                src?.PerformSkillMotion();
                attack.Target.AttachFloatingTextIndicator("<font-weight=300><cspace=-0.5>SLOW", TextIndicatorType.Debuff, 0.2f);
                AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_decagility.ogg", attack.Target.gameObject);
                AgiDownEffect.LaunchEffect(attack.Target);
            }
            else
                src?.SpriteAnimator?.ChangeMotion(SpriteMotion.Idle);
        }
    }
}