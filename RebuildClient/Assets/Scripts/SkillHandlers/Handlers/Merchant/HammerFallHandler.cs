using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.HammerFall)]
    public class HammerFallHandler : SkillHandlerBase
    {
        public override int GetSkillAoESize(ServerControllable src, int lvl) => 2;

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformBasicAttackMotion(); //server will send the hammerfall effect separately
            
            if(src.CharacterType == CharacterType.Player)
                src.FloatingDisplay.ShowChatBubbleMessage("Hammer Fall!!");
        }
    }
}