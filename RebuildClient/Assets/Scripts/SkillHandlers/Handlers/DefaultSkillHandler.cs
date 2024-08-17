using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    public class DefaultSkillHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            if(attack.Damage > 0)
                attack.Target?.Messages.SendHitEffect(src, attack.MotionTime);
        }
    }
}