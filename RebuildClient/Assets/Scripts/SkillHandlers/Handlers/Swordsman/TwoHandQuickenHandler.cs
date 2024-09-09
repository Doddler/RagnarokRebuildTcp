using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.TwoHandQuicken)]
    public class TwoHandQuickenHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            CameraFollower.Instance.AttachEffectToEntity("TwoHandQuicken", src.gameObject, src.Id);

            
            // if(src.CharacterType == CharacterType.Player)
            //     src.FloatingDisplay.ShowChatBubbleMessage("Two-Hand Quicken" + "!!");
        }
    }
}