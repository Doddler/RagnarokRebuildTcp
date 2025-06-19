using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.CartRevolution)]
    public class CartRevolutionHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target.Messages.SendHitEffect(attack.Src, attack.MotionTime, 1, attack.HitCount);
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            //src.SetAttackAnimationSpeed(attack.MotionTime);
            src?.PerformSkillMotion();
            if(src != null)
                CartRevolutionEffect.CreateCartRevolution(src, 0);
            if(attack.Target != null)
                CartRevolutionEffect.CreateCartRevolution(attack.Target, attack.DamageTiming);
        }
    }
}