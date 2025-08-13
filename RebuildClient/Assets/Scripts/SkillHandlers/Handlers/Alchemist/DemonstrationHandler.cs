using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Demonstration, true)]
    public class DemonstrationHandler : SkillHandlerBase
    {
        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if(src.CharacterType == CharacterType.Monster)
                src.PerformBasicAttackMotion();
        }
    }
}