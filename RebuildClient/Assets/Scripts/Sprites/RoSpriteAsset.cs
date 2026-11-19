using System.IO;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Assets.Scripts.Sprites
{
    [MovedFrom(true, sourceClassName: "RoSprAsset")]
    public class RoSpriteAsset : ScriptableObject
    {
        private const int PaletteGridSize = 16;
        private const int PaletteColorCount = PaletteGridSize * PaletteGridSize;

        [HideInInspector] public string sprVersion;
        [HideInInspector] public string filepath;
        [HideInInspector] public string sprFileName;
        [HideInInspector] public Texture2D palette;
        [HideInInspector] public Texture2D[] images;
        [HideInInspector] public int indexedImageCount;

        public void Load(string path)
        {
            filepath = path;
            sprFileName = Path.GetFileNameWithoutExtension(path);
            name = sprFileName;
            var data = new RoSpr(path);
            var paletteColors = data.PaletteColors;
            sprVersion = data.Version;
            var indexed = data.BitmapImages;
            indexedImageCount = indexed.Length;
            images = new Texture2D[indexed.Length + data.TrueColorImages.Length];
            palette = CreateTexture(PaletteGridSize, PaletteGridSize, sprFileName + "_palette");
            for (var i = 0; i < PaletteColorCount; i++)
            {
                var color = paletteColors[i];
                color.a = byte.MaxValue;
                palette.SetPixel(i % PaletteGridSize, PaletteGridSize - 1 - i / PaletteGridSize, color);
            }
            palette.Apply();
            for (var i = 0; i < indexed.Length; i++)
            {
                var frame = indexed[i];
                var texture = CreateTexture(frame.ImageWidth, frame.ImageHeight, $"image_{i:D4}");
                for (var pixel = 0; pixel < frame.PaletteIndexes.Length; pixel++)
                {
                    var index = frame.PaletteIndexes[pixel];
                    var color = paletteColors[index];
                    color.a = index == 0 ? (byte)0 : byte.MaxValue;
                    texture.SetPixel(pixel % frame.ImageWidth, frame.ImageHeight - 1 - pixel / frame.ImageWidth, color);
                }
                texture.Apply();
                images[i] = texture;
            }
            for (var i = 0; i < data.TrueColorImages.Length; i++)
            {
                var frame = data.TrueColorImages[i];
                var texture = CreateTexture(frame.ImageWidth, frame.ImageHeight, $"image_{i + indexed.Length:D4}");
                // True-color SPR pixels are stored bottom-up, matching Unity's SetPixels32.
                texture.SetPixels32(frame.ImageData);
                texture.Apply();
                images[i + indexed.Length] = texture;
            }
        }

        private static Texture2D CreateTexture(int width, int height, string textureName)
        {
            if (width <= 0 || height <= 0 || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
                throw new InvalidDataException($"Unsupported SPR image dimensions: {width} x {height}.");
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }
    }
}
