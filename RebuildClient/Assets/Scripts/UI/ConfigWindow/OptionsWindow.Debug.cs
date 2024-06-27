using Assets.Scripts.Network;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public partial class OptionsWindow : WindowBase
    {
        public void AdjustCharacterLevel(int level) => NetworkManager.Instance.SendAdminLevelUpRequest(level);
        public void ToggleHide()
        {
            var isHidden = CameraFollower.Instance.TargetControllable.IsHidden;
            NetworkManager.Instance.SendAdminHideCharacter(!isHidden);
        }
        
    }
}