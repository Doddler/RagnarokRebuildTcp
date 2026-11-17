using Assets.Scripts.UI;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum.EntityStats;
using TMPro;
using UnityEngine;
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
        public Toggle ClassicUiStyleToggle;

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
            if (ClassicUiStyleToggle != null)
                ClassicUiStyleToggle.SetIsOnWithoutNotify(GameConfig.Data.UiStyle == UiStyle.Classic);
        }

        public void UpdateUISizeSettings()
        {
            GameConfig.Data.MasterUIScale = MasterUISize.value / 10f;
            uiUpdateEvent = FinalizeUISizeUpdate;
        }

        public void UpdateUIStyleSettings()
        {
            if (!isInitialized || ClassicUiStyleToggle == null)
                return;

            var style = ClassicUiStyleToggle.isOn ? UiStyle.Classic : UiStyle.Modern;
            GameConfig.SetUiStyle(style);
            GameConfig.SaveConfig();
        }

        public void UpdateUICharacterWindowSettings()
        {
            GameConfig.Data.ShowBaseExpValue = BaseExpShowValue.isOn;
            GameConfig.Data.ShowBaseExpPercent = BaseExpShowPercent.isOn;
            GameConfig.Data.ShowJobExpValue = JobExpShowValue.isOn;
            GameConfig.Data.ShowJobExpPercent = JobExpShowPercent.isOn;
            GameConfig.Data.ShowExpGainInChat = ShowExpGainInChat.isOn;

            var state = PlayerState.Instance;
            CameraFollower.Instance.UpdatePlayerExp(state.BaseExp, CameraFollower.Instance.ExpForLevel(state.BaseLevel));
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
            EnsureUIStyleToggle();
            Refresh();

            if (ClassicUiStyleToggle != null)
            {
                ClassicUiStyleToggle.onValueChanged.RemoveListener(OnUIStyleToggleChanged);
                ClassicUiStyleToggle.onValueChanged.AddListener(OnUIStyleToggleChanged);
            }
        }


        private void EnsureUIStyleToggle()
        {
            if (ClassicUiStyleToggle != null || ShowExpGainInChat == null)
                return;

            var go = Instantiate(ShowExpGainInChat.gameObject, ShowExpGainInChat.transform.parent);
            go.name = "Classic UI Style Toggle";
            ClassicUiStyleToggle = go.GetComponent<Toggle>();
            ClassicUiStyleToggle.onValueChanged = new Toggle.ToggleEvent();

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = "Classic UI Style";
        }
        private void OnUIStyleToggleChanged(bool _)
        {
            UpdateUIStyleSettings();
        }
    }
}