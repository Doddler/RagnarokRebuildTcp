using System.Linq;
using Assets.Scripts.Sprites;
using Scripts.Sprites;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Assets.Scripts.Editor
{
    [ScriptedImporter(1, "spr", AllowCaching = true)]
    public sealed class RoSprAssetImporter : ScriptedImporter
    {
        // This allows us to avoid fiddling with project settings
        // to add support for the file extension
        private static void TryIncludeSprExtension()
        {
            if (EditorSettings.projectGenerationUserExtensions.Contains(SprExtension)) return;
            var list = EditorSettings.projectGenerationUserExtensions.ToList();
            list.Add(SprExtension);
            EditorSettings.projectGenerationUserExtensions = list.ToArray();
        }
        
        public string spriteVersion;
        public string filepath;
        public string sprFileName;
        public Texture2D palette;
        public Texture2D atlas;
        public Rect[]  atlasRects;
        
        private RoSprAsset asset;
        private const string SprExtension = "spr";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            asset = ScriptableObject.CreateInstance<RoSprAsset>();
            asset.Load(ctx.assetPath);
            
            ctx.AddObjectToAsset(GUID.Generate().ToString(), asset.atlas);
            ctx.AddObjectToAsset(GUID.Generate().ToString(), asset.palette);
            
            PopulateImporterFields();
            
            ctx.AddObjectToAsset(GUID.Generate().ToString(), asset);
            ctx.SetMainObject(asset);

            TryIncludeSprExtension();
        }
        
        private void PopulateImporterFields()
        {
            spriteVersion = asset.spriteVersion;
            filepath = asset.filepath;
            sprFileName = asset.sprFileName;
            palette = asset.palette;
            atlas = asset.atlas;
            atlasRects = asset.atlasRects;
        }
    }

    [CustomEditor(typeof(RoSprAssetImporter))]
    public sealed class RoSprAssetImporterEditor : ScriptedImporterEditor
    {
        public override bool showImportedObject
        {
            get
            {
                return false;
            }
        }
    }
}