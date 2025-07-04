using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.EnergyCoat)]
    public class EnergyCoatHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (src == null)
                return;
            
            src.PerformSkillMotion();
            CameraFollower.Instance.AttachEffectToEntity("EnergyCoat", src.gameObject, src.Id);
            //
            // if(src.CharacterType == CharacterType.Player)
            //     src.FloatingDisplay.ShowChatBubbleMessage("Improve Concentration" + "!!");
        }
    }
}