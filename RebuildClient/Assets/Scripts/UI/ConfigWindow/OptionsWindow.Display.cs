using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public partial class OptionsWindow : WindowBase
    {
        public Toggle SpriteFilteringToggle;
        public Toggle UseSpriteBasedDamageNumbersToggle;
        public Toggle AllowTabToShowWalkTableToggle;
        public Toggle HideShoutChatToggle;
        public Toggle EnableXRayToggle;
        
        public void UpdateDisplayOptions()
        {
            GameConfig.Data.UseUnfilteredSprites = !SpriteFilteringToggle.isOn;
            GameConfig.Data.UseSpriteBasedDamageNumbers = UseSpriteBasedDamageNumbersToggle.isOn;
            GameConfig.Data.AllowTabToShowWalkTable = AllowTabToShowWalkTableToggle.isOn;
            GameConfig.Data.HideShoutChat = HideShoutChatToggle.isOn;
            
            GameConfig.Data.EnableXRay = EnableXRayToggle.isOn;

            CameraFollower.Instance.UseTTFDamage = !GameConfig.Data.UseSpriteBasedDamageNumbers;
            CameraFollower.Instance.SetSmoothPixel(!GameConfig.Data.UseUnfilteredSprites);
        }

        private void InitializeDisplayOptions()
        {
            CameraFollower.Instance.UseTTFDamage = !GameConfig.Data.UseSpriteBasedDamageNumbers;
            CameraFollower.Instance.SetSmoothPixel(!GameConfig.Data.UseUnfilteredSprites);

            SpriteFilteringToggle.isOn = !GameConfig.Data.UseUnfilteredSprites;
            UseSpriteBasedDamageNumbersToggle.isOn = GameConfig.Data.UseSpriteBasedDamageNumbers;
            AllowTabToShowWalkTableToggle.isOn = GameConfig.Data.AllowTabToShowWalkTable;
            HideShoutChatToggle.isOn = GameConfig.Data.HideShoutChat;
            
            EnableXRayToggle.isOn = GameConfig.Data.EnableXRay;
        }
    }
}