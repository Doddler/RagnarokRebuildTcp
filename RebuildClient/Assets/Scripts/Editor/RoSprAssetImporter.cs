using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Assets.Scripts.Editor
{
    [ScriptedImporter(1, "spr", AllowCaching = true)]
    public sealed class RoSprAssetImporter : ScriptedImporter
    {

        private RoSprAsset asset;
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var tempAsset = ScriptableObject.CreateInstance<RoSprAsset>();
            tempAsset.Load(ctx.assetPath);
            ctx.AddObjectToAsset(GUID.Generate().ToString(), tempAsset);
            ctx.SetMainObject(tempAsset);
        }
    }
}