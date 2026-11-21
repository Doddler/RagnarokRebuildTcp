using System;
using System.IO;
using Assets.Scripts.PlayerControl;
using UnityEngine;

namespace Assets.Scripts.UI.ConfigWindow
{
    public static partial class GameConfig
    {
        public static GameConfigData Data;

        private static bool isInitialized;
        private static string configPath => Path.Combine(Application.persistentDataPath, "config.txt");

        public static void InitializeIfNecessary()
        {
            if (isInitialized)
                return;

            //FromJsonOverwrite only writes fields present in the file; the rest keep their defaults
            Data = GameConfigData.CreateDefault();

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
                    //a throw can leave a partial overwrite, so reset to defaults
                    Debug.LogError($"Could not load config file from {configPath}, using default values instead.");
                    Data = GameConfigData.CreateDefault();
                }
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
