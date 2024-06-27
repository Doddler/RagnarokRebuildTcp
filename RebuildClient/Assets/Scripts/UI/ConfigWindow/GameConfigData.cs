using System;
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
    public class GameConfigData
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
        public float DamageNumberSize = 0.75f;
        public bool ShowExpGainOnKill = true;
        public bool ShowMonsterHpBars = true;
        public bool AutoHideFullHPBars = false;
        public bool ScalePlayerDisplayWithZoom = true;
        public bool ShowLevelsInOverlay = true;
        //ui
        public float MasterUIScale = 1f;

        public HotBarSaveData[] HotBarSaveData;
        
        public void InitDefaultValues()
        {
            AudioVolumeLevels = new int[4];
            AudioMuteValues = new bool[4];
            HotBarSaveData = new HotBarSaveData[30];

            AudioVolumeLevels[(int)ConfigAudioChannel.Master] = 25;
            AudioVolumeLevels[(int)ConfigAudioChannel.Music] = 70;
            AudioVolumeLevels[(int)ConfigAudioChannel.Effects] = 50;
            AudioVolumeLevels[(int)ConfigAudioChannel.Environment] = 50;

            for (var i = 0; i < 4; i++)
                AudioMuteValues[i] = false;
        }
    }
}