using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.HammerFall)]
    public class HammerFallHandler : SkillHandlerBase
    {
        public override int GetSkillAoESize(ServerControllable src, int lvl) => 2;

        public override void ExecuteSkillGroundTargeted(ServerControllable src, Vector2Int target, int lvl)
        {
            src.PerformBasicAttackMotion(); //server will send the hammerfall effect separately
            
            if(src.CharacterType == CharacterType.Player)
                src.FloatingDisplay.ShowChatBubbleMessage("Hammer Fall!!");
        }
    }
}