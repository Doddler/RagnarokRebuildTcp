using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    public class DefaultSkillHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ServerControllable target, int lvl)
        {
            DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
        }
    }
}