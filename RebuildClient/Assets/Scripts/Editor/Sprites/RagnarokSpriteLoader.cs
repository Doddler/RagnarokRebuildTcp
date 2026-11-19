using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Sprites
{
    internal sealed class RagnarokSpriteLoader : IDisposable
    {
        private readonly RoBitmapImage[] indexedImages;
        private readonly RoTrueColorImage[] trueColorImages;
        private readonly Color32[] paletteColors;
        private readonly List<Texture2D> textures = new();

        public List<Sprite> Sprites { get; } = new();
        public List<Vector2Int> SpriteSizes { get; } = new();
        public int SpriteFrameCount => textures.Count;
        public Texture2D Atlas { get; private set; }

        public RagnarokSpriteLoader(RoBitmapImage[] indexedImages, RoTrueColorImage[] trueColorImages, Color32[] paletteColors)
        {
            this.indexedImages = indexedImages;
            this.trueColorImages = trueColorImages;
            this.paletteColors = paletteColors;
        }

        internal static void ValidateImages(RoBitmapImage[] indexed, RoTrueColorImage[] trueColor)
        {
            if (indexed.Length == 0 && trueColor.Length == 0)
                throw new InvalidDataException("SPR contains no images.");
            foreach (var image in indexed)
                ValidateDimensions(image.ImageWidth, image.ImageHeight);
            foreach (var image in trueColor)
                ValidateDimensions(image.ImageWidth, image.ImageHeight);
        }

        public void Dispose()
        {
            foreach (var texture in textures)
                UnityEngine.Object.DestroyImmediate(texture);
        }

        private void ExtendTransparentPixels(Color[] colors, int width, int height)
        {
            // Preserve the original atlas edge bleed so runtime filtering remains unchanged.
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = x + y * width;
                if (!Mathf.Approximately(colors[index].a, 0)) continue;

                var count = 0;
                var r = 0f;
                var g = 0f;
                var b = 0f;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    var neighbor = colors[nx + ny * width];
                    if (Mathf.Approximately(neighbor.a, 0)) continue;
                    count++;
                    r += neighbor.r;
                    g += neighbor.g;
                    b += neighbor.b;
                }

                if (count > 0) colors[index] = new Color(r / count, g / count, b / count, 0);
            }
        }

        private Texture2D IndexedToTexture(RoBitmapImage frame)
        {
            var image = CreateTexture(frame.ImageWidth, frame.ImageHeight);
            var colors = new Color[frame.PaletteIndexes.Length];
            for (var y = 0; y < frame.ImageHeight; y++)
            for (var x = 0; x < frame.ImageWidth; x++)
            {
                var paletteIndex = frame.PaletteIndexes[x + y * frame.ImageWidth];
                var color = paletteColors[paletteIndex];
                color.a = paletteIndex == 0 ? (byte)0 : byte.MaxValue;
                colors[x + (frame.ImageHeight - y - 1) * frame.ImageWidth] = color;
            }
            ExtendTransparentPixels(colors, frame.ImageWidth, frame.ImageHeight);
            image.SetPixels(colors);
            return image;
        }

        private Texture2D TrueColorToTexture(RoTrueColorImage frame)
        {
            var image = CreateTexture(frame.ImageWidth, frame.ImageHeight);
            var colors = new Color[frame.ImageData.Length];
            for (var i = 0; i < colors.Length; i++) colors[i] = frame.ImageData[i];
            ExtendTransparentPixels(colors, frame.ImageWidth, frame.ImageHeight);
            image.SetPixels(colors);
            return image;
        }

        private static void ValidateDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0 || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
                throw new InvalidDataException($"Unsupported SPR image dimensions: {width} x {height}.");
        }

        private static Texture2D CreateTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                alphaIsTransparency = true
            };
        }

        private Texture2D CreateTextureForImage(int frameIndex)
        {
            return frameIndex < indexedImages.Length
                ? IndexedToTexture(indexedImages[frameIndex])
                : TrueColorToTexture(trueColorImages[frameIndex - indexedImages.Length]);
        }

        private void LoadTextures(string baseName, int frame)
        {
            var image = CreateTextureForImage(frame);
            image.name = $"{baseName}_{frame:D4}";
            image.hideFlags = HideFlags.HideInHierarchy;
            textures.Add(image);
        }

        public void Load(string basename, string atlasPath, RoSpriteData dataObject)
        {
            var frameCount = indexedImages.Length + trueColorImages.Length;
            for (var i = 0; i < frameCount; i++) LoadTextures(basename, i);

            var rects = CreateAtlas(atlasPath);
            ReconcileSprites(basename, rects, dataObject);

            if (atlasPath.Contains("Shields") || atlasPath.Contains("status-curse"))
            {
                dataObject.ReverseSortingWhenFacingNorth = true;
                dataObject.IgnoreAnchor = true;
            }
        }

        private Rect[] CreateAtlas(string atlasPath)
        {
            var texture = new Texture2D(2, 2) { name = Path.GetFileNameWithoutExtension(atlasPath) };
            try
            {
                var rects = texture.PackTextures(textures.ToArray(), 2, 2048, false);
                texture.filterMode = FilterMode.Bilinear;

                var compression = atlasPath.Contains("/Icons/")
                    ? TextureImporterCompression.Uncompressed
                    : TextureImporterCompression.CompressedHQ;
                Atlas = TextureImportHelper.SaveAndUpdateTexture(texture, atlasPath, importer =>
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.textureCompression = compression;
                    importer.crunchedCompression = false;
                });
                return rects;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private void ReconcileSprites(string basename, Rect[] rects, RoSpriteData dataObject)
        {
            for (var i = 0; i < rects.Length; i++)
            {
                var texRect = new Rect(rects[i].x * Atlas.width, rects[i].y * Atlas.height,
                    rects[i].width * Atlas.width, rects[i].height * Atlas.height);
                SpriteSizes.Add(new Vector2Int(textures[i].width, textures[i].height));
                var sprite = Sprite.Create(Atlas, texRect, new Vector2(0.5f, 0.5f), 50, 0, SpriteMeshType.FullRect);
                sprite.name = $"sprite_{basename}_{i:D4}";

                var previous = dataObject.Sprites != null && i < dataObject.Sprites.Length ? dataObject.Sprites[i] : null;
                if (previous != null && AssetDatabase.GetAssetPath(previous) == AssetDatabase.GetAssetPath(dataObject))
                {
                    EditorUtility.CopySerialized(sprite, previous);
                    UnityEngine.Object.DestroyImmediate(sprite);
                    sprite = previous;
                    EditorUtility.SetDirty(sprite);
                }
                else AssetDatabase.AddObjectToAsset(sprite, dataObject);
                Sprites.Add(sprite);
            }

            if (dataObject.Sprites == null) return;
            var assetPath = AssetDatabase.GetAssetPath(dataObject);
            for (var i = rects.Length; i < dataObject.Sprites.Length; i++)
            {
                var obsolete = dataObject.Sprites[i];
                if (obsolete != null && AssetDatabase.GetAssetPath(obsolete) == assetPath)
                    UnityEngine.Object.DestroyImmediate(obsolete, true);
            }
        }
    }
}
