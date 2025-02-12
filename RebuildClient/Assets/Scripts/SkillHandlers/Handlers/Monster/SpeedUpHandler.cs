using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.SpeedUp)]
    public class SpeedUpHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Holy));
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();
            attack.Target?.AttachFloatingTextIndicator("<font-weight=300><cspace=-0.5>AGI UP!");
            if(attack.Target != null)
                AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_incagility.ogg", src.gameObject);
        }
    }
}