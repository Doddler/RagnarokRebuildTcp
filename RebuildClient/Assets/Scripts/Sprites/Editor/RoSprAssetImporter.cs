using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Scripts.Sprites.Editor
{
    [ScriptedImporter(1, "spr", AllowCaching = true)]
    public sealed class RoSprAssetImporter : ScriptedImporter
    {
        private RoSprAsset asset;

        public string spriteVersion;
        public string filepath;
        public string sprName;
        public Texture2D palette;
        public Texture2D atlas;
        //public Rect[]  atlasRects;
        
        private const string SprExtension = "spr";

        private void PopulateImporterFields()
        {
            spriteVersion = asset.spriteVersion;
            filepath = asset.filepath;
            sprName = asset.sprName;
            palette = asset.palette;
            atlas = asset.atlas;
            //atlasRects = asset.atlasRects;
        }
        
        // This allows us to avoid fiddling with project settings
        private static void TryIncludeSprExtension()
        {
            if (EditorSettings.projectGenerationUserExtensions.Contains(SprExtension)) return;
            var list = EditorSettings.projectGenerationUserExtensions.ToList();
            list.Add(SprExtension);
            EditorSettings.projectGenerationUserExtensions = list.ToArray();
        }
        
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