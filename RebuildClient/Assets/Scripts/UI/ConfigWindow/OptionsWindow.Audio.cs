using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public partial class OptionsWindow : WindowBase
    {
        //audio options
        public AudioMixer Mixer;
        public Slider AudioMaster;
        public Slider AudioBgm;
        public Slider AudioEnvironment;
        public Slider AudioEffects;
        public Toggle ToggleAudioMaster;
        public Toggle ToggleAudioBgm;
        public Toggle ToggleAudioEnvironment;
        public Toggle ToggleAudioEffects;

        private bool isAudioInitialized;
        
        public void InitializeAudio()
        {
            AudioMaster.value = GameConfig.GetVolumeForAudioChannel(ConfigAudioChannel.Master);
            AudioBgm.value = GameConfig.GetVolumeForAudioChannel(ConfigAudioChannel.Music);
            AudioEnvironment.value = GameConfig.GetVolumeForAudioChannel(ConfigAudioChannel.Environment);
            AudioEffects.value = GameConfig.GetVolumeForAudioChannel(ConfigAudioChannel.Effects);

            ToggleAudioMaster.isOn = GameConfig.GetMuteStatusForAudioChannel(ConfigAudioChannel.Master);
            ToggleAudioBgm.isOn = GameConfig.GetMuteStatusForAudioChannel(ConfigAudioChannel.Music);
            ToggleAudioEnvironment.isOn = GameConfig.GetMuteStatusForAudioChannel(ConfigAudioChannel.Environment);
            ToggleAudioEffects.isOn = GameConfig.GetMuteStatusForAudioChannel(ConfigAudioChannel.Effects);

            isAudioInitialized = true;
            RefreshAudioLevels();
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
            if (!isAudioInitialized)
                return;
            
            GameConfig.SetVolumeForAudioChannel(ConfigAudioChannel.Master, Mathf.RoundToInt(AudioMaster.value));
            GameConfig.SetVolumeForAudioChannel(ConfigAudioChannel.Music, Mathf.RoundToInt(AudioBgm.value));
            GameConfig.SetVolumeForAudioChannel(ConfigAudioChannel.Environment, Mathf.RoundToInt(AudioEnvironment.value));
            GameConfig.SetVolumeForAudioChannel(ConfigAudioChannel.Effects, Mathf.RoundToInt(AudioEffects.value));
            
            GameConfig.SetMuteStatusForAudioChannel(ConfigAudioChannel.Master, ToggleAudioMaster.isOn);
            GameConfig.SetMuteStatusForAudioChannel(ConfigAudioChannel.Music, ToggleAudioBgm.isOn);
            GameConfig.SetMuteStatusForAudioChannel(ConfigAudioChannel.Environment, ToggleAudioEnvironment.isOn);
            GameConfig.SetMuteStatusForAudioChannel(ConfigAudioChannel.Effects, ToggleAudioEffects.isOn);
            
            Mixer.SetFloat("Master", LinearToDecibel(ToggleAudioMaster.isOn ? 0 : AudioMaster.value / 100f));
            Mixer.SetFloat("Music",  LinearToDecibel(ToggleAudioBgm.isOn ? 0 : AudioBgm.value / 100f));
            Mixer.SetFloat("Environment",  LinearToDecibel(ToggleAudioEnvironment.isOn ? 0 : AudioEnvironment.value / 100f));
            Mixer.SetFloat("Sounds",  LinearToDecibel(ToggleAudioEffects.isOn ? 0 : AudioEffects.value / 100f));
            
            // Debug.Log($"{AudioMaster.value} {AudioBgm.value} {AudioEffects.value} {AudioEffects.value}");
        }
    }
}
