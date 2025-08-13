using System;
using System.IO;
using Assets.Scripts.PlayerControl;
using UnityEngine;
using File = System.IO.File;

namespace Assets.Scripts.UI.ConfigWindow
{
    public static class GameConfig
    {
        public static GameConfigData Data;

        public static int GetVolumeForAudioChannel(ConfigAudioChannel channel) => Data.AudioVolumeLevels[(int)channel];
        public static bool GetMuteStatusForAudioChannel(ConfigAudioChannel channel) => Data.AudioMuteValues[(int)channel];
        public static void SetVolumeForAudioChannel(ConfigAudioChannel channel, int volume) => Data.AudioVolumeLevels[(int)channel] = volume;
        public static void SetMuteStatusForAudioChannel(ConfigAudioChannel channel, bool isMuted) => Data.AudioMuteValues[(int)channel] = isMuted;
        
        private static bool isInitialized;
        private static string configPath => Path.Combine(Application.persistentDataPath, "config.txt");

        public static void InitializeIfNecessary()
        {
            if (isInitialized)
                return;
            
            Data = new GameConfigData();
            Data.InitDefaultValues();

            if (File.Exists(configPath))
            {
                try
                {
                    Debug.Log($"Loading config from path {configPath}");
                    string configText = File.ReadAllText(configPath);
                    JsonUtility.FromJsonOverwrite(configText, Data);
                }
                catch (Exception)
                {
                    Debug.LogError($"Could not load config file from {configPath}, using default values instead.");

                }
            }
            else
            {
                Data = new GameConfigData();
                Data.InitDefaultValues();
            }


            isInitialized = true;
        }

        public static void SaveConfig()
        {
            UiManager.Instance.SyncFloatingBoxPositionsWithSaveData();
            var charName = PlayerState.Instance?.PlayerName;
            if(!string.IsNullOrWhiteSpace(charName))
                UiManager.Instance.SkillHotbar.SaveHotBarData(Data.GetHotBarDataForCharacter(charName));
            
            Debug.Log($"Saving game configuration to {configPath}");
            if (!isInitialized)
            {
                Debug.LogError($"Cannot save game config as it is not yet initialized.");
                return;
            }
            
            var text = JsonUtility.ToJson(Data);
            File.WriteAllText(configPath, text);
        }
        
    }
}