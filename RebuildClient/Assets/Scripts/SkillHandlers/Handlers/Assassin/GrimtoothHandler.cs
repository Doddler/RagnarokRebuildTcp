using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Effects.EffectHandlers.Skills.Assassin;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers.Assassin
{
    [SkillHandler(CharacterSkill.Grimtooth)]
    public class GrimtoothHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if(src?.SpriteAnimator?.IsHidden ?? false)
                src.PerformSkillMotion();

            if (src != null && attack.Target != null) 
                GrimtoothTrailEffect.Create(src, attack.Target, attack.MotionTime);
        }
    }
}