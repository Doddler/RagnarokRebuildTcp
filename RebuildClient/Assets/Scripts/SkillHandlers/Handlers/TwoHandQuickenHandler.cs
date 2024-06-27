using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.TwoHandQuicken)]
    public class TwoHandQuickenHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ServerControllable target, int lvl)
        {
            CameraFollower.Instance.AttachEffectToEntity("TwoHandQuicken", src.gameObject, src.Id);
            src.SpriteAnimator.Color = new Color(1, 1, 0.5f);
            RoSpriteTrailManager.Instance.AttachTrailToEntity(src);
            
            if(src.CharacterType == CharacterType.Player)
                src.FloatingDisplay.ShowChatBubbleMessage("Two-Hand Quicken" + "!!");
        }
    }
}