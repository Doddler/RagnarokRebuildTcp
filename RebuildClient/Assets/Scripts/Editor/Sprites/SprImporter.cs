using Assets.Scripts.Sprites;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Assets.Scripts.Editor.Sprites
{
    [ScriptedImporter(3, "spr")]
    public sealed class SprImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var asset = ScriptableObject.CreateInstance<RoSpriteAsset>();
            asset.Load(ctx.assetPath);
            ctx.AddObjectToAsset("palette", asset.palette);
            for (var i = 0; i < asset.images.Length; i++)
                ctx.AddObjectToAsset($"image_{i:D4}", asset.images[i]);
            ctx.AddObjectToAsset("spr", asset);
            ctx.SetMainObject(asset);
        }
    }
}
