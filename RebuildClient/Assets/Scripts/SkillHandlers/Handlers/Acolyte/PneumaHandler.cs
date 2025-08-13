using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Pneuma, true)]
    public class PneumaHandler : SkillHandlerBase
    {
        public override int GetSkillAoESize(ServerControllable src, int lvl) => 2;

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformSkillMotion();
            if(src.CharacterType == CharacterType.Player)
                src.FloatingDisplay.ShowChatBubbleMessage("Pneuma!!");
        }
    }
}