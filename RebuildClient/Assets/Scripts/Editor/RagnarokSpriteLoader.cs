using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    struct SpriteFrameData
    {
        public bool IsIndexed;
        public int Width;
        public int Height;
        public byte[] Data;
    }

    class RagnarokSpriteLoader
    {
        //private Stream fs;
        private MemoryStream ms;
        private BinaryReader br;

        private int version;
        private int rgbaCount;

        private List<SpriteFrameData> spriteFrames;
        private byte[] paletteData;

        public List<Texture2D> Textures = new();
        public List<Sprite> Sprites = new();
        public List<Vector2Int> SpriteSizes = new();
        public int SpriteFrameCount
        {
            get
            {
                return spriteFrames.Count;
            }
        }

        public Texture2D Atlas;

        public int IndexCount { get; private set; }

        private void ReadIndexedImage()
        {
            for (var i = 0; i < IndexCount; i++)
            {
                var width = br.ReadUInt16();
                var height = br.ReadUInt16();
                var data = br.ReadBytes(width * height);

                var frame = new SpriteFrameData()
                {
                    IsIndexed = true,
                    Width = width,
                    Height = height,
                    Data = data
                };

                spriteFrames.Add(frame);
            }
        }


        private void ReadRleIndexedImage()
        {
            for (var i = 0; i < IndexCount; i++)
            {
                var width = br.ReadUInt16();
                var height = br.ReadUInt16();
                var size = width * height;
                var data = new byte[size];
                var index = 0;
                var end = br.ReadUInt16() + ms.Position;

                while (ms.Position < end)
                {
                    var c = br.ReadByte();
                    data[index++] = c;

                    if (c == 0)
                    {
                        var count = br.ReadByte();
                        if (count == 0)
                        {
                            data[index++] = count;
                        }
                        else
                        {
                            for (var j = 1; j < count; j++)
                            {
                                data[index++] = c;
                            }
                        }
                    }
                }

                var frame = new SpriteFrameData()
                {
                    IsIndexed = true,
                    Width = width,
                    Height = height,
                    Data = data
                };

                spriteFrames.Add(frame);
            }
        }

        private void ReadRgbaImage()
        {
            for (var i = 0; i < rgbaCount; i++)
            {
                var width = br.ReadInt16();
                var height = br.ReadInt16();

                var frame = new SpriteFrameData()
                {
                    IsIndexed = false,
                    Width = width,
                    Height = height,
                    Data = br.ReadBytes(width * height * 4)
                };

                spriteFrames.Add(frame);
            }
        }

        private static void ExtendSpriteTextureData(Color[] colors, SpriteFrameData frame)
        {
            //we're going to extend the sprite color into the transparent area around the sprite
            //this is to make bilinear filtering work good with the sprite

            for (var y = 0; y < frame.Height; y++)
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    var count = 0;
                    var r = 0f;
                    var g = 0f;
                    var b = 0f;

                    var color = colors[x + y * frame.Width];
                    if (!Mathf.Approximately(color.a, 0))
                        continue;

                    for (var y2 = -1; y2 <= 1; y2++)
                    {
                        for (var x2 = -1; x2 <= 1; x2++)
                        {
                            if (y + y2 < 0 || y + y2 >= frame.Height)
                                continue;
                            if (x + x2 < 0 || x + x2 >= frame.Width)
                                continue;
                            var color2 = colors[x + x2 + (y + y2) * frame.Width];

                            if (Mathf.Approximately(color2.a, 0))
                                continue;

                            count++;

                            r += color2.r;
                            g += color2.g;
                            b += color2.b;
                        }
                    }

                    if (count > 0)
                    {
                        colors[x + y * frame.Width] = new Color(r / count, g / count, b / count, 0);
                    }
                }
            }
        }

        private static Texture2D RgbaToTexture(SpriteFrameData frame)
        {
            var image = new Texture2D(frame.Width, frame.Height, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                alphaIsTransparency = true
            };

            var colors = new Color[frame.Width * frame.Height];

            for (var y = 0; y < frame.Height; y++)
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    var pos = (x + (frame.Height - y - 1) * frame.Width) * 4;

                    var r = frame.Data[pos + 3];
                    var g = frame.Data[pos + 2];
                    var b = frame.Data[pos + 1];
                    var a = frame.Data[pos + 0];

                    var color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);

                    colors[x + (frame.Height - y - 1) * frame.Width] = color;
                }
            }

            ExtendSpriteTextureData(colors, frame);

            image.SetPixels(colors);

            return image;
        }

        private Texture2D IndexedToTexture(SpriteFrameData frame)
        {
            var image = new Texture2D(frame.Width, frame.Height, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                alphaIsTransparency = true
            };

            var colors = new Color[frame.Width * frame.Height];

            for (var y = 0; y < frame.Height; y++)
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    var index1 = frame.Data[x + y * frame.Width] * 4;

                    var r = paletteData[index1 + 0];
                    var g = paletteData[index1 + 1];
                    var b = paletteData[index1 + 2];
                    var a = index1 > 0 ? 255 : 0;

                    var color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);


                    colors[x + (frame.Height - y - 1) * frame.Width] = color;
                }
            }

            ExtendSpriteTextureData(colors, frame);

            image.SetPixels(colors);

            return image;
        }

        private void ReadPalette()
        {
            paletteData = br.ReadBytes(1024);
        }

        public Texture2D LoadFirstSpriteTextureOnly(string sprPath)
        {
            var basename = Path.GetFileNameWithoutExtension(sprPath);

            var bytes = File.ReadAllBytes(sprPath);
            ms = new MemoryStream(bytes);
            br = new BinaryReader(ms);


            var header = new string(br.ReadChars(2));
            if (header != "SP")
                throw new Exception("Not sprite");

            var minorVersion = br.ReadByte();
            var majorVersion = br.ReadByte();
            version = majorVersion * 10 + minorVersion;

            IndexCount = br.ReadUInt16();
            rgbaCount = 0;

            if (version > 11)
                rgbaCount = br.ReadUInt16();

            int frameCount = IndexCount + rgbaCount;

            spriteFrames = new List<SpriteFrameData>(frameCount);

            if (version < 21)
                ReadIndexedImage();
            else
                ReadRleIndexedImage();

            ReadRgbaImage();

            if (version > 10)
                ReadPalette();

            if (spriteFrames.Count <= 0)
                return null;
            Texture2D image = spriteFrames[0].IsIndexed ? IndexedToTexture(spriteFrames[0]) : RgbaToTexture(spriteFrames[0]);
            image.name = basename;
            return image;

        }

        private void LoadTextures(string baseName, int frame, int paletteId = -1)
        {
            Texture2D image;
            if (spriteFrames[frame].IsIndexed)
                image = IndexedToTexture(spriteFrames[frame]);
            else
                image = RgbaToTexture(spriteFrames[frame]);
            if (paletteId >= 0)
                image.name = $"{baseName}_{frame:D4}";
            else
                image.name = $"{baseName}_{frame:D4}_p{paletteId}";
            image.hideFlags = HideFlags.HideInHierarchy;

            Textures.Add(image);
        }

        public void Load(string filename, string atlasPath, RoSpriteData dataObject, string paletteFile, string imfPath = null)
        {
            var basename = Path.GetFileNameWithoutExtension(filename);

            if (!File.Exists(filename))
            {
                throw new FileNotFoundException($"File {filename} not found");
            }
            
            var bytes = File.ReadAllBytes(filename);
            ms = new MemoryStream(bytes);
            br = new BinaryReader(ms);
            
            var header = new string(br.ReadChars(2));
            if (header != "SP")
                throw new Exception("Not sprite");

            var minorVersion = br.ReadByte();
            var majorVersion = br.ReadByte();
            version = majorVersion * 10 + minorVersion;

            IndexCount = br.ReadUInt16();
            rgbaCount = 0;

            if (version > 11)
                rgbaCount = br.ReadUInt16();

            int frameCount = IndexCount + rgbaCount;

            spriteFrames = new List<SpriteFrameData>(frameCount);

            if (version < 21)
                ReadIndexedImage();
            else
                ReadRleIndexedImage();

            ReadRgbaImage();

            if (version > 10)
                ReadPalette();

            if (File.Exists(paletteFile))
            {
                var origPalette = paletteData;
                var newPaletteData = File.ReadAllBytes(paletteFile);

                var tr = newPaletteData[255 * 4];
                var tb = newPaletteData[255 * 4 + 1];
                var tg = newPaletteData[255 * 4 + 2];

                for (var i = 0; i < newPaletteData.Length; i += 4)
                {
                    var r = newPaletteData[i + 0];
                    var g = newPaletteData[i + 1];
                    var b = newPaletteData[i + 2];
                    if (r == tr && g == tb && b == tg)
                        Array.Copy(origPalette, i, paletteData, i, 4);
                    else
                        Array.Copy(newPaletteData, i, paletteData, i, 4);
                }

                for (var i = 0; i < spriteFrames.Count; i++)
                    LoadTextures(basename, i);
            }
            else
            {
                for (var i = 0; i < spriteFrames.Count; i++)
                    LoadTextures(basename, i);

            }

            var superTexture = new Texture2D(2, 2)
            {
                name = Path.GetFileNameWithoutExtension(atlasPath)
            };
            Rect[] rects = superTexture.PackTextures(Textures.ToArray(), 2, 2048, false);
            superTexture.filterMode = FilterMode.Bilinear;
            
            var compression = TextureImporterCompression.CompressedHQ;
            if (atlasPath.Replace("\\", "/").Contains("/Icons/"))
                compression = TextureImporterCompression.Uncompressed;
                
            superTexture = TextureImportHelper.SaveAndUpdateTexture(superTexture, atlasPath, ti =>
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.textureCompression = compression;
                ti.crunchedCompression = false;
            });

            Atlas = superTexture;

            for (var i = 0; i < rects.Length; i++)
            {
                var texRect = new Rect(rects[i].x * superTexture.width, rects[i].y * superTexture.height, rects[i].width * superTexture.width,
                    rects[i].height * superTexture.height);
                SpriteSizes.Add(new Vector2Int(Textures[i].width, Textures[i].height));
                var sprite = Sprite.Create(superTexture, texRect, new Vector2(0.5f, 0.5f), 50, 0, SpriteMeshType.FullRect);

                sprite.name = $"sprite_{basename}_{i:D4}";

                AssetDatabase.AddObjectToAsset(sprite, dataObject);

                Sprites.Add(sprite);
            }

            if (atlasPath.Contains("Shields") || atlasPath.Contains("status-curse"))
            {
                dataObject.ReverseSortingWhenFacingNorth = true;
                dataObject.IgnoreAnchor = true;
            }
            
            br.Dispose();
            ms.Dispose();
        }
    }
}