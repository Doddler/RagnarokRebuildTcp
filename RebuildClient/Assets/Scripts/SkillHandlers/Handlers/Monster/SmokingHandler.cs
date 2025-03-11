using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Smoking)]
    public class SmokingHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            //hardcoded to orc warrior...
            if (target.ClassId == 4116 && target.SpriteAnimator != null && target.StatusEffectState.HasStatusEffect(CharacterStatusEffect.Smoking))
            {
                target.SpriteAnimator.ChangeMotion(SpriteMotion.Performance3);
                target.SpriteAnimator.ForceLoop = true;
            }
        }
    }
}