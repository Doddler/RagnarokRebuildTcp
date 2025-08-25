namespace Editor
{
    [ScriptedImporter(1, "spr", AllowCaching = true)]
    public sealed class RoSprAssetImporter : ScriptedImporter
    {

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var asset = ScriptableObject.CreateInstance<RoSprAsset>();
            ctx.AddObjectToAsset(GUID.Generate().ToString(), asset);
            ctx.SetMainObject(asset);
        }
    }
}