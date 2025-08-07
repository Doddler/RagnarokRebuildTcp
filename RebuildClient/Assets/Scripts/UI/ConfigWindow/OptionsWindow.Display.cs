using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public partial class OptionsWindow : WindowBase
    {
        public Toggle SpriteFilteringToggle;
        public Toggle UseSpriteBasedDamageNumbersToggle;
        public Toggle AllowTabToShowWalkTableToggle;
        
        public void UpdateDisplayOptions()
        {
            GameConfig.Data.UseUnfilteredSprites = !SpriteFilteringToggle.isOn;
            GameConfig.Data.UseSpriteBasedDamageNumbers = UseSpriteBasedDamageNumbersToggle.isOn;
            GameConfig.Data.AllowTabToShowWalkTable = AllowTabToShowWalkTableToggle.isOn;

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
        }
    }
}