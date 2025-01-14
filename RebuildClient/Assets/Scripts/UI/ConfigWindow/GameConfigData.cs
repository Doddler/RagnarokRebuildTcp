using System;
using System.Collections.Generic;
using Assets.Scripts.Utility;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.Serialization;

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
        public bool ShowExpGainOnKill = true;
        public bool ShowMonsterHpBars = true;
        public bool AutoHideFullHPBars = false;
        public bool ScalePlayerDisplayWithZoom = true;
        public bool ShowLevelsInOverlay = true;
        //ui
        public float MasterUIScale = 0.75f;
        //visuals
        public bool UseSmoothPixel = true;
        
        //storage
        public Vector2 StoragePosition = Vector2.zero;
        public int LastStorageTab = 0;
        public int LastPlayedVersion = 0;
        public string LastViewedPatchNotes = "";

        //this is stupid but unity won't serialize dictionaries so we gotta do it ourselves
        public List<string> CharacterNames = new();
        public List<HotBarSaveData> AllHotBarData = new();
        
        [NonSerialized] public Dictionary<string, HotBarSaveData[]> CharacterHotBarData = new();

        //user
        [CanBeNull] public string SavedLoginToken;

        public HotBarSaveData[] GetHotBarDataForCharacter(string name)
        {
            if (CharacterHotBarData.TryGetValue(name, out var data))
                return data;

            var hotBarData = new HotBarSaveData[30];
            CharacterHotBarData.Add(name, hotBarData);

            return hotBarData;
        }

        public void InitDefaultValues()
        {
            AudioVolumeLevels = new int[4];
            AudioMuteValues = new bool[4];
            // HotBarSaveData = new HotBarSaveData[30];

            AudioVolumeLevels[(int)ConfigAudioChannel.Master] = 25;
            AudioVolumeLevels[(int)ConfigAudioChannel.Music] = 70;
            AudioVolumeLevels[(int)ConfigAudioChannel.Effects] = 50;
            AudioVolumeLevels[(int)ConfigAudioChannel.Environment] = 50;

            for (var i = 0; i < 4; i++)
                AudioMuteValues[i] = false;
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

        public void OnAfterDeserialize()
        {
                        
            for(var j = 0; j < CharacterNames.Count; j++)
            {
                var name = CharacterNames[j];
                if (!CharacterHotBarData.TryGetValue(name, out var hotBar))
                {
                    hotBar = new HotBarSaveData[30];
                    CharacterHotBarData.Add(name, hotBar);
                }

                for (var i = 0; i < 30; i++)
                    hotBar[i] = AllHotBarData[j * 30 + i];
            }

        }
    }
}