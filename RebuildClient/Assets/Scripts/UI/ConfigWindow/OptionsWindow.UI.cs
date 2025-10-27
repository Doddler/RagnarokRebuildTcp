using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public partial class OptionsWindow
    {
        public Slider MasterUISize;
        public Toggle BaseExpShowValue;
        public Toggle BaseExpShowPercent;
        public Toggle JobExpShowValue;
        public Toggle JobExpShowPercent;
        public Toggle ShowExpGainInChat;

        // Refreshes the Config UI. Used when internally changing settings and wanting the UI to refresh to
        // show the newly applied values
        public void Refresh()
        {
            MasterUISize.SetValueWithoutNotify(GameConfig.Data.MasterUIScale * 10);
            BaseExpShowValue.SetIsOnWithoutNotify(GameConfig.Data.ShowBaseExpValue);
            BaseExpShowPercent.SetIsOnWithoutNotify(GameConfig.Data.ShowBaseExpPercent);
            JobExpShowValue.SetIsOnWithoutNotify(GameConfig.Data.ShowJobExpValue);
            JobExpShowPercent.SetIsOnWithoutNotify(GameConfig.Data.ShowJobExpPercent);
            ShowExpGainInChat.SetIsOnWithoutNotify(GameConfig.Data.ShowExpGainInChat);
        }

        public void UpdateUISizeSettings()
        {
            GameConfig.Data.MasterUIScale = MasterUISize.value / 10f;
            uiUpdateEvent = FinalizeUISizeUpdate;
        }

        public void UpdateUICharacterWindowSettings()
        {
            GameConfig.Data.ShowBaseExpValue = BaseExpShowValue.isOn;
            GameConfig.Data.ShowBaseExpPercent = BaseExpShowPercent.isOn;
            GameConfig.Data.ShowJobExpValue = JobExpShowValue.isOn;
            GameConfig.Data.ShowJobExpPercent = JobExpShowPercent.isOn;
            GameConfig.Data.ShowExpGainInChat = ShowExpGainInChat.isOn;
            
            var state = PlayerState.Instance;
            CameraFollower.Instance.UpdatePlayerExp(state.Exp, CameraFollower.Instance.ExpForLevel(state.Level));
            CameraFollower.Instance.UpdatePlayerJobExp(state.GetData(PlayerStat.JobExp), 
            ClientDataLoader.Instance.GetJobExpRequired(state.JobId, state.GetData(PlayerStat.JobLevel)));
        }

        private void FinalizeUISizeUpdate()
        {
            CameraFollower.Instance.UpdateCameraSize();
            uiUpdateEvent = UiManager.Instance.FitFloatingWindowsIntoPlayArea;
        }

        private void InitializeUISettings()
        {
            Refresh();
        }
    }
}