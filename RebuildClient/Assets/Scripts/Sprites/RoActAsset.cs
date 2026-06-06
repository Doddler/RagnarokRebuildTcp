using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Scripts.Sprites
{
    // RoActData || Animation
    // RoActData.RoAnimationClip || AnimationClip
    // RoActData.RoAnimationFrame.RoSpriteLayer || AnimationCurve
    // RoActData.RoAnimationEvents || AnimationClip.events
    
    public class RoActAsset : ScriptableObject
    {
        public int InstanceID
        {
            get
            {
                if (instanceID == 0)
                    instanceID = GetInstanceID();
                return instanceID;
            }
        }
        public int HashCode
        {
            get { return InstanceID.GetHashCode(); }
        }

        [HideInInspector] public string actVersion;
        [HideInInspector] public string filePath;
        [HideInInspector] public string actFileName;
        [HideInInspector] public List<AnimationClip> animationClips;
        [HideInInspector] public RoSprAsset spr;
        
        private int instanceID;

        public void Load(string assetFilePath)
        {
            Debug.Log($"Getting spr asset");
            spr = AssetDatabase.LoadAssetAtPath<RoSprAsset>(Path.ChangeExtension(filePath, "spr"));
            filePath = assetFilePath;
            actFileName = Path.GetFileNameWithoutExtension(filePath);
            animationClips = new List<AnimationClip>();

            Debug.Log($"Reading act at {filePath}");
            var rawActData = new RoAct(filePath);
            actVersion = rawActData.Version;
        }
    }
}