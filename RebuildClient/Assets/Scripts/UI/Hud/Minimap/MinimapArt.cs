using System.Collections;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    //The art's sprite covers only the drawn part of its texture; older minimaps cover all of it.
    public class MinimapArt
    {
        //per axis, in both the art and the walk mask
        public const int PixelsPerTile = 2;

        private const string Folder = "Assets/Maps/minimap";
        private static readonly int SecondaryTex = Shader.PropertyToID("_SecondaryTex");

        private AsyncOperationHandle<Sprite> artHandle;
        private AsyncOperationHandle<Sprite> walkHandle;

        public bool IsLoaded { get; private set; }
        public Sprite Art { get; private set; }
        //light where the tile can't be walked on
        public Sprite WalkMask { get; private set; }
        public int TileWidth { get; private set; }
        public int TileHeight { get; private set; }
        //the drawn part in tiles, y from the bottom
        public RectInt Content { get; private set; }

        private static string ArtPath(string mapName) => $"{Folder}/{mapName}.png";
        private static string ArtSpriteAddress(string mapName) => $"{ArtPath(mapName)}[{mapName}]";
        private static string WalkMaskPath(string mapName) => $"{Folder}/{mapName}_walkmask.png";

        public IEnumerator Load(string mapName)
        {
            Release();

            var walkPath = WalkMaskPath(mapName);
            if (!ClientDataLoader.DoesAddressableExist<Sprite>(ArtPath(mapName)) || !ClientDataLoader.DoesAddressableExist<Sprite>(walkPath))
                yield break;

            artHandle = Addressables.LoadAssetAsync<Sprite>(ArtSpriteAddress(mapName));
            walkHandle = Addressables.LoadAssetAsync<Sprite>(walkPath);
            yield return artHandle;
            yield return walkHandle;
            if (artHandle.Status != AsyncOperationStatus.Succeeded || walkHandle.Status != AsyncOperationStatus.Succeeded)
                yield break;

            Art = artHandle.Result;
            WalkMask = walkHandle.Result;
            TileWidth = Art.texture.width / PixelsPerTile;
            TileHeight = Art.texture.height / PixelsPerTile;

            var rect = Art.rect;
            Content = new RectInt(
                Mathf.RoundToInt(rect.x / PixelsPerTile), Mathf.RoundToInt(rect.y / PixelsPerTile),
                Mathf.RoundToInt(rect.width / PixelsPerTile), Mathf.RoundToInt(rect.height / PixelsPerTile));
            IsLoaded = true;
        }

        //Anchors the art over the drawn part of the parent, which spans the map at one unit per tile.
        public void ShowOn(Image image, Material material, bool darkenUnwalkable = true)
        {
            image.sprite = Art;
            if (material != null)
            {
                image.material = material;
                material.SetTexture(SecondaryTex, darkenUnwalkable ? WalkMask.texture : Texture2D.blackTexture);
            }

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(Content.xMin / (float)TileWidth, Content.yMin / (float)TileHeight);
            rect.anchorMax = new Vector2(Content.xMax / (float)TileWidth, Content.yMax / (float)TileHeight);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        //also cancels a load still in progress
        public void Release()
        {
            if (artHandle.IsValid())
                Addressables.Release(artHandle);
            if (walkHandle.IsValid())
                Addressables.Release(walkHandle);
            artHandle = default;
            walkHandle = default;
            IsLoaded = false;
            Art = null;
            WalkMask = null;
        }
    }
}
