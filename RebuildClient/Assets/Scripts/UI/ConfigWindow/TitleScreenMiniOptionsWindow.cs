using System;
using System.Collections.Generic;
using Assets.Scripts.UI.TitleScreen;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public class TitleScreenMiniOptionsWindow : WindowBase
    {
        public LoginBox LoginBox;
        public RectTransform LoginRect;
        public AudioMixer Mixer;
        public Slider AudioMaster;
        public Slider AudioBgm;
        public Slider MasterUISize;
        public Toggle ToggleAudioMaster;
        public Toggle ToggleAudioBgm;
        public Button OptionsButton;
        
        private Action uiUpdateEvent;

        public void Awake()
        {
            GameConfig.InitializeIfNecessary();
            InitializeOptions();
            OptionsButton.interactable = false;
        }

        public override void HideWindow()
        {
            OptionsButton.interactable = true;
            if(LoginBox.gameObject.activeInHierarchy && !RectTransformUtility.RectangleContainsScreenPoint(LoginRect, Input.mousePosition))
                LoginBox.ReturnFocus();
            base.HideWindow();
        }

        public void InitializeOptions()
        {
            AudioMaster.value = GameConfig.GetVolumeForAudioChannel(ConfigAudioChannel.Master);
            AudioBgm.value = GameConfig.GetVolumeForAudioChannel(ConfigAudioChannel.Music);
            ToggleAudioMaster.isOn = GameConfig.GetMuteStatusForAudioChannel(ConfigAudioChannel.Master);
            ToggleAudioBgm.isOn = GameConfig.GetMuteStatusForAudioChannel(ConfigAudioChannel.Music);
            MasterUISize.SetValueWithoutNotify(GameConfig.Data.MasterUIScale * 10);
        }
        
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
        
        private float LinearToDecibel(float linear)
        {
            float dB;
		
            if (linear != 0)
                dB = 20.0f * Mathf.Log10(linear);
            else
                dB = -144.0f;
		
            return dB;
        }

        public void RefreshAudioLevels()
        {
            GameConfig.SetVolumeForAudioChannel(ConfigAudioChannel.Master, Mathf.RoundToInt(AudioMaster.value));
            GameConfig.SetVolumeForAudioChannel(ConfigAudioChannel.Music, Mathf.RoundToInt(AudioBgm.value));
            
            GameConfig.SetMuteStatusForAudioChannel(ConfigAudioChannel.Master, ToggleAudioMaster.isOn);
            GameConfig.SetMuteStatusForAudioChannel(ConfigAudioChannel.Music, ToggleAudioBgm.isOn);
            
            Mixer.SetFloat("Master", LinearToDecibel(ToggleAudioMaster.isOn ? 0 : AudioMaster.value / 100f));
            Mixer.SetFloat("Music",  LinearToDecibel(ToggleAudioBgm.isOn ? 0 : AudioBgm.value / 100f));
        }
        
        public void Update()
        {
            if (uiUpdateEvent != null && !Input.GetMouseButton(0))
            {
                //the update event might set a new update event, so... we wait
                var update = uiUpdateEvent;
                uiUpdateEvent = null;
                update();
            }
            
            if (Input.GetKeyDown(KeyCode.Escape) || (Input.GetMouseButtonUp(0) && !IsPointerOverUIObject()))
                HideWindow();
        }
    }
}