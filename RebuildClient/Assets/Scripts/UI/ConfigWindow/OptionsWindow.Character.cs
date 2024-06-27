using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public partial class OptionsWindow : WindowBase
    {
        //skill
        public Toggle ShowAllSkills;
        public Toggle AutoLockSkillWindow;
        //character
        public Slider DamageSizeSlider;
        public Toggle ShowExpGainOnKill;
        public Toggle ScalePlayerHud;
        public Toggle ShowMonsterHpBars;
        public Toggle ShowLevelsInName;
        public Toggle AutoHideHpBars;

        public void UpdateSkillSettings()
        {
            GameConfig.Data.AutoLockSkillWindow = AutoLockSkillWindow.isOn;
        }
        
        public void UpdateCharacterSettings()
        {
            if (!isInitialized) return; //we'll catch notifications early from Initialize if we don't do this
            GameConfig.Data.DamageNumberSize = DamageSizeSlider.value;
            GameConfig.Data.ShowExpGainOnKill = ShowExpGainOnKill.isOn;
            GameConfig.Data.ShowMonsterHpBars = ShowMonsterHpBars.isOn;
            GameConfig.Data.ScalePlayerDisplayWithZoom = ScalePlayerHud.isOn;
            GameConfig.Data.ShowLevelsInOverlay = ShowLevelsInName.isOn;
            GameConfig.Data.AutoHideFullHPBars = AutoHideHpBars.isOn;
        }

        private void InitializeCharacterOptions()
        {
            //skill
            ShowAllSkills.SetIsOnWithoutNotify(GameConfig.Data.ShowAllSkillsInSkillWindow);
            AutoLockSkillWindow.SetIsOnWithoutNotify(GameConfig.Data.AutoLockSkillWindow);
            //character
            DamageSizeSlider.value = GameConfig.Data.DamageNumberSize;
            ShowExpGainOnKill.SetIsOnWithoutNotify(GameConfig.Data.ShowExpGainOnKill);
            ScalePlayerHud.SetIsOnWithoutNotify(GameConfig.Data.ScalePlayerDisplayWithZoom);
            ShowMonsterHpBars.SetIsOnWithoutNotify(GameConfig.Data.ShowMonsterHpBars);
            ShowLevelsInName.SetIsOnWithoutNotify(GameConfig.Data.ShowLevelsInOverlay);
            AutoHideHpBars.SetIsOnWithoutNotify(GameConfig.Data.AutoHideFullHPBars);
        }
    }
}