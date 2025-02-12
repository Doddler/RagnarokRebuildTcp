using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.PowerUp)]
    public class PowerUpHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            //HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Ghost));
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if(src.CharacterType != CharacterType.Monster)
                src.PerformSkillMotion();
        }
    }
}