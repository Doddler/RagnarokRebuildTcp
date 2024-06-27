using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public partial class OptionsWindow
    {
        public Slider MasterUISize;

        public void UpdateUISizeSettings()
        {
            GameConfig.Data.MasterUIScale = MasterUISize.value / 10f;
            uiUpdateEvent = FinalizeUISizeUpdate;
        }

        private void FinalizeUISizeUpdate()
        {
            CameraFollower.Instance.UpdateCameraSize();
            uiUpdateEvent = UiManager.Instance.FitFloatingWindowsIntoPlayArea;
        }

        private void InitializeUISettings()
        {
            MasterUISize.SetValueWithoutNotify(GameConfig.Data.MasterUIScale * 10);
        }
    }
}