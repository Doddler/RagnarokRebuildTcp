using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Assets.Scripts.UI.ConfigWindow
{
    public enum ConfigAudioChannel
    {
        Master,
        Music,
        Effects,
        Environment
    }

    [Serializable]
    public class HotBarSaveData
    {
        public DragItemType Type;
        public int ItemId;
        public int ItemCount;
        public byte[] UniqueItem;
    }
    
    [Serializable]
    public class GameConfigData : ISerializationCallbackReceiver
    {
        //settings
        public int[] WindowSizes;
        public Vector2[] WindowPositions;
        //audio
        public int[] AudioVolumeLevels;
        public bool[] AudioMuteValues;
        //skills
        public bool AutoLockSkillWindow = false;
        public bool ShowAllSkillsInSkillWindow = false;
        //character overlay
        public float DamageNumberSize = 0.85f;
        public float DamageSpacingSize = 0.7f;
        public bool ShowExpGainOnKill = true;
        public bool ShowMonsterHpBars = true;
        //players outside the party; the player's own and party members' bars always show
        public bool ShowOtherPlayerHpBars = false;
        public bool ShowPartyNameInOverlay = false;
        public bool ScalePlayerDisplayWithZoom = true;
        public bool AdjustOverlayWhenSitting = true;
        //ui
        public float MasterUIScale = 0.8f;
        public bool ScaleUiWithResolution = true;
        public bool KeepWindowsOnScreen = true;
        public bool EnableMinimap = true;
        public float MinimapOpacity = 0.85f;
        public float MinimapIconOpacity = 1f;
        //in UI units, and the lines shown above the input line
        public float ChatWidth = 720f;
        public float ChatLines = 8f;

        public bool ShowExpGainInChat = false;

        //visuals
        public bool UseSmoothPixel = true;

        public bool LimitForegroundFps = true;
        public float ForegroundFpsLimit = 240f;
        public bool LimitBackgroundFps = true;
        public float BackgroundFpsLimit = 30f;

        public bool EnableSpriteFiltering = false;
        public bool UseSpriteBasedDamageNumbers = false;
        public bool AllowTabToShowWalkTable = false;
        public bool HideShoutChat = false;
        
        public bool EnableXRay = false;
        public bool EnableFog = true;

        //game
        public bool EnableWASDControls = false;

        //storage
        public Vector2 StoragePosition = Vector2.zero;
        public int LastStorageTab = 0;
        public int LastPlayedVersion = 0;
        public string LastViewedPatchNotes = "";
        public int LastUsedCharacterSlot = 0;

        //this is stupid but unity won't serialize dictionaries so we gotta do it ourselves
        public List<string> CharacterNames = new();
        public List<HotBarSaveData> AllHotBarData = new();
        
        [NonSerialized] public Dictionary<string, HotBarSaveData[]> CharacterHotBarData = new();

        //user
        [CanBeNull] public string SavedLoginToken;

        public const int HotBarSlots = 30;

        private static readonly int ChannelCount = Enum.GetValues(typeof(ConfigAudioChannel)).Length;

        public HotBarSaveData[] GetHotBarDataForCharacter(string name)
        {
            if (CharacterHotBarData.TryGetValue(name, out var data))
                return data;

            var hotBarData = new HotBarSaveData[HotBarSlots];
            CharacterHotBarData.Add(name, hotBarData);

            return hotBarData;
        }

        //The audio arrays need a length before they can be filled.
        public static GameConfigData CreateDefault()
        {
            var data = new GameConfigData();
            data.InitDefaultValues();
            return data;
        }

        private void InitDefaultValues()
        {
            // HotBarSaveData = new HotBarSaveData[30];
            AudioVolumeLevels = new int[ChannelCount];
            AudioMuteValues = new bool[ChannelCount];

            AudioVolumeLevels[(int)ConfigAudioChannel.Master] = 25;
            AudioVolumeLevels[(int)ConfigAudioChannel.Music] = 70;
            AudioVolumeLevels[(int)ConfigAudioChannel.Effects] = 50;
            AudioVolumeLevels[(int)ConfigAudioChannel.Environment] = 50;
        }

        public void OnBeforeSerialize()
        {
            CharacterNames.Clear();
            AllHotBarData.Clear();
            
            foreach (var (name, hotbar) in CharacterHotBarData)
            {
                CharacterNames.Add(name);
                foreach(var h in hotbar)
                    AllHotBarData.Add(h);
            }

        }

        //The file's lists can differ in length; a throw here drops the whole config.
        public void OnAfterDeserialize()
        {
            for (var j = 0; j < CharacterNames.Count; j++)
            {
                var name = CharacterNames[j];
                if (!CharacterHotBarData.TryGetValue(name, out var hotBar))
                {
                    hotBar = new HotBarSaveData[HotBarSlots];
                    CharacterHotBarData.Add(name, hotBar);
                }

                for (var i = 0; i < HotBarSlots; i++)
                {
                    var index = j * HotBarSlots + i;
                    if (index >= AllHotBarData.Count)
                        return; //remaining names keep empty bars

                    hotBar[i] = AllHotBarData[index];
                }
            }
        }
    }
}
