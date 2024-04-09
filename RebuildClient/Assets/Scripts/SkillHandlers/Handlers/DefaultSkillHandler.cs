using Assets.Scripts.Network;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    public class DefaultSkillHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ServerControllable target, int lvl)
        {
            src.PerformBasicAttackMotion();
        }
    }
}