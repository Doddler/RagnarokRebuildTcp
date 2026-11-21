using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;

namespace Assets.Scripts.UI.ConfigWindow
{
    public class OptionsWindow : WindowBase
    {
        private bool isInitialized;

        public void Initialize()
        {
            if (isInitialized)
                return;

            GameConfig.InitializeIfNecessary();
            GameConfig.ApplyAll();
            isInitialized = true;
        }

        public void Refresh()
        {
            foreach (var toggle in GetComponentsInChildren<ToggleOption>(true))
                toggle.SyncFromConfig();
            foreach (var slider in GetComponentsInChildren<SliderOption>(true))
                slider.SyncFromConfig();
            foreach (var dropdown in GetComponentsInChildren<DropdownOption>(true))
                dropdown.SyncFromConfig();
        }

        public void ResetToDefaults()
        {
            UiManager.Instance.YesNoOptionsWindow.BeginPrompt("Reset all options to their default values?", "Reset", "Cancel",
                () => { GameConfig.ResetToDefaults(); Refresh(); }, null, false);
        }

        public void AdjustCharacterLevel(int level) => NetworkManager.Instance.SendAdminLevelUpRequest(level, false);

        public void ToggleHide()
        {
            NetworkManager.Instance.SendAdminHideCharacter(!PlayerState.Instance.IsAdminHidden);
        }

        public override void OnDestroy()
        {
            GameConfig.SaveConfig();
            base.OnDestroy();
        }
    }
}
