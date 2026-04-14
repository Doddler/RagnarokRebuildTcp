using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Assets.Scripts.Editor.ActHandling
{
    [ScriptedImporter(1, ext:"act", AllowCaching = true)]
    public class RoActAssetImporter : ScriptedImporter
    {
        public string actVersion;
        public string filePath;
        public string actFileName;
        public List<AnimationClip> animationClips;
        
        private static void TryIncludeActExtension()
        {
            if (EditorSettings.projectGenerationUserExtensions.Contains(ActExtension)) return;
            var list = EditorSettings.projectGenerationUserExtensions.ToList();
            list.Add(ActExtension);
            EditorSettings.projectGenerationUserExtensions = list.ToArray();
        }
        
        private RoActAsset asset;
        private const string ActExtension = "act";
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            asset = ScriptableObject.CreateInstance<RoActAsset>();
            Debug.Log($"Loading asset on {ctx.assetPath}");
            asset.Load(ctx.assetPath);
            
            PopulateImporterFields();
            
            ctx.AddObjectToAsset(GUID.Generate().ToString(), asset);
            ctx.SetMainObject(asset);
            
            TryIncludeActExtension();
        }

        private void PopulateImporterFields()
        {
            actVersion = asset.actVersion;
            filePath = asset.filePath;
            actFileName = asset.actFileName;
            animationClips = asset.animationClips;
        }
        
        // [CustomEditor(typeof(RoActAssetImporter))]
        // public sealed class RoActAssetImporterEditor : ScriptedImporterEditor
        // {
        //     public override bool showImportedObject
        //     {
        //         get { return false; }
        //     }
        // }
    }
}