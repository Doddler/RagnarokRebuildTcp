using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    internal struct SpriteFrameData
    {
        public bool IsIndexed;
        public int Width;
        public int Height;
        public byte[] Data;
    }

    internal class RagnarokSpriteLoader
    {
        //private Stream fs;
        private MemoryStream _ms;
        private BinaryReader _br;

        private int _version;
        private int _rgbaCount;

        private List<SpriteFrameData> _spriteFrames;
        private byte[] _paletteData;

        public readonly List<Texture2D> Textures = new();
        public readonly List<Sprite> Sprites = new();
        public readonly List<Vector2Int> SpriteSizes = new();
        public int SpriteFrameCount => _spriteFrames.Count;

        public Texture2D Atlas;

        public int IndexCount { get; private set; }

        private void ReadIndexedImage()
        {
            for (var i = 0; i < IndexCount; i++)
            {
                var width = _br.ReadUInt16();
                var height = _br.ReadUInt16();
                var data = _br.ReadBytes(width * height);

                var frame = new SpriteFrameData()
                {
                    IsIndexed = true,
                    Width = width,
                    Height = height,
                    Data = data
                };

                _spriteFrames.Add(frame);
            }
        }


        private void ReadRleIndexedImage()
        {
            for (var i = 0; i < IndexCount; i++)
            {
                var width = _br.ReadUInt16();
                var height = _br.ReadUInt16();
                var size = width * height;
                var data = new byte[size];
                var index = 0;
                var end = _br.ReadUInt16() + _ms.Position;

                while (_ms.Position < end)
                {
                    var c = _br.ReadByte();
                    data[index++] = c;

                    if (c == 0)
                    {
                        var count = _br.ReadByte();
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

                _spriteFrames.Add(frame);
            }
        }

        private void ReadRgbaImage()
        {
            for (var i = 0; i < _rgbaCount; i++)
            {
                var width = _br.ReadInt16();
                var height = _br.ReadInt16();

                var frame = new SpriteFrameData()
                {
                    IsIndexed = false,
                    Width = width,
                    Height = height,
                    Data = _br.ReadBytes(width * height * 4)
                };

                _spriteFrames.Add(frame);
            }
        }

        private void ExtendSpriteTextureData(Color[] colors, SpriteFrameData frame)
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

        private Texture2D RgbaToTexture(SpriteFrameData frame)
        {
            var image = new Texture2D(frame.Width, frame.Height, TextureFormat.ARGB32, false);
            image.wrapMode = TextureWrapMode.Clamp;
            image.alphaIsTransparency = true;

            var colors = new Color[frame.Width * frame.Height];

            //Debug.Log(frame.Width + " " + frame.Height);

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
            var image = new Texture2D(frame.Width, frame.Height, TextureFormat.ARGB32, false);
            image.wrapMode = TextureWrapMode.Clamp;
            image.alphaIsTransparency = true;

            var colors = new Color[frame.Width * frame.Height];

            for (var y = 0; y < frame.Height; y++)
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    var index1 = frame.Data[x + y * frame.Width] * 4;

                    var r = _paletteData[index1 + 0];
                    var g = _paletteData[index1 + 1];
                    var b = _paletteData[index1 + 2];
                    var a = index1 > 0 ? 255 : 0;

                    var color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);


                    colors[x + (frame.Height - y - 1) * frame.Width] = color;
                    //image.SetPixel(x, frame.Height - y - 1, color);
                }
            }

            ExtendSpriteTextureData(colors, frame);

            image.SetPixels(colors);

            return image;
        }

        private void ReadPalette()
        {
            _paletteData = _br.ReadBytes(1024);
        }

        public Texture2D LoadFirstSpriteTextureOnly(string sprPath)
        {
            var filename = sprPath;
            var basename = Path.GetFileNameWithoutExtension(filename);

            var bytes = File.ReadAllBytes(filename);
            _ms = new MemoryStream(bytes);
            _br = new BinaryReader(_ms);


            var header = new string(_br.ReadChars(2));
            if (header != "SP")
                throw new Exception("Not sprite");

            var minorVersion = _br.ReadByte();
            var majorVersion = _br.ReadByte();
            _version = majorVersion * 10 + minorVersion;

            IndexCount = _br.ReadUInt16();
            _rgbaCount = 0;

            if (_version > 11)
                _rgbaCount = _br.ReadUInt16();

            //Debug.Log($"RGBA count: {rgbaCount}");

            var frameCount = IndexCount + _rgbaCount;

            _spriteFrames = new List<SpriteFrameData>(frameCount);

            if (_version < 21)
                ReadIndexedImage();
            else
                ReadRleIndexedImage();

            ReadRgbaImage();

            if (_version > 10)
                ReadPalette();

            if (_spriteFrames.Count > 0)
            {
                var i = 0;

                Texture2D image;
                if (_spriteFrames[i].IsIndexed)
                    image = IndexedToTexture(_spriteFrames[i]);
                else
                    image = RgbaToTexture(_spriteFrames[i]);
                image.name = basename;
                // image.name = $"{basename}_{i:D4}";
                // image.hideFlags = HideFlags.HideInHierarchy;

                //ctx.AddObjectToAsset(image.name, image);

                //var sprite = Sprite.Create(image, new Rect(0, 0, indexedFrames[i].Width, indexedFrames[i].Height),
                //    new Vector2(0.5f, 0.5f), 100);

                //sprite.name = $"sprite_{basename}_{i:D4}";

                //ctx.AddObjectToAsset(sprite.name, sprite);

                return image;
                //Sprites.Add(sprite);
            }

            return null;
        }

        private void LoadTextures(string baseName, int frame, int paletteId = -1)
        {
            Texture2D image;
            if (_spriteFrames[frame].IsIndexed)
                image = IndexedToTexture(_spriteFrames[frame]);
            else
                image = RgbaToTexture(_spriteFrames[frame]);
            if (paletteId >= 0)
                image.name = $"{baseName}_{frame:D4}";
            else
                image.name = $"{baseName}_{frame:D4}_p{paletteId}";
            image.hideFlags = HideFlags.HideInHierarchy;

            Textures.Add(image);
        }

        public void Load(string filename, string atlasPath, RoSpriteData dataObject, string paletteFile, string imfPath = null)
        {
            //var filename = ctx.assetPath;
            var basename = Path.GetFileNameWithoutExtension(filename);

            if (!File.Exists(filename))
            {
                throw new FileNotFoundException($"File {filename} not found");
            }
            
            var bytes = File.ReadAllBytes(filename);
            _ms = new MemoryStream(bytes);
            _br = new BinaryReader(_ms);

            //fs = new FileStream(filename, FileMode.Open);
            //br = new BinaryReader(fs);

            var header = new string(_br.ReadChars(2));
            if (header != "SP")
                throw new Exception("Not sprite");

            var minorVersion = _br.ReadByte();
            var majorVersion = _br.ReadByte();
            _version = majorVersion * 10 + minorVersion;

            IndexCount = _br.ReadUInt16();
            _rgbaCount = 0;

            if (_version > 11)
                _rgbaCount = _br.ReadUInt16();

            //Debug.Log($"RGBA count: {rgbaCount}");

            var frameCount = IndexCount + _rgbaCount;

            _spriteFrames = new List<SpriteFrameData>(frameCount);

            if (_version < 21)
                ReadIndexedImage();
            else
                ReadRleIndexedImage();

            ReadRgbaImage();

            if (_version > 10)
                ReadPalette();

            // Debug.Log($"Palette check " + Path.Combine(dirName, "Palette/", basename + "_0.pal"));


            //var palPath = Path.Combine("G:\\Games\\RagnarokJP\\data\\palette\\몸\\costume_1", $"{basename}_0_1.pal");

            //Debug.Log(palPath);

            if (File.Exists(paletteFile))
            {
                var origPalette = _paletteData;
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
                        Array.Copy(origPalette, i, _paletteData, i, 4);
                    else
                        Array.Copy(newPaletteData, i, _paletteData, i, 4);
                }

                for (var i = 0; i < _spriteFrames.Count; i++)
                    LoadTextures(basename, i);
            }
            else
            {
                for (var i = 0; i < _spriteFrames.Count; i++)
                    LoadTextures(basename, i);

            }

            var supertexture = new Texture2D(2, 2);
            supertexture.name = Path.GetFileNameWithoutExtension(atlasPath);
            var rects = supertexture.PackTextures(Textures.ToArray(), 2, 2048, false);
            supertexture.filterMode = FilterMode.Bilinear;

            //var atlasDir = Path.Combine(dirName, "atlas/");
            //var atlasPath = Path.Combine(atlasDir, supertexture.name + "_.png");
            var compression = TextureImporterCompression.CompressedHQ;
            if (atlasPath.Replace("\\", "/").Contains("/Icons/"))
                compression = TextureImporterCompression.Uncompressed;
                
            supertexture = TextureImportHelper.SaveAndUpdateTexture(supertexture, atlasPath, ti =>
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.textureCompression = compression;
                ti.crunchedCompression = false;
            });
            //
            //var bytes2 = supertexture.EncodeToPNG();
            //File.WriteAllBytes(atlasPath, bytes2);
            //supertexture.Compress(true);


            //ctx.AddObjectToAsset(supertexture.name, supertexture);


            Atlas = supertexture;

            //var byteData = supertexture.EncodeToPNG();
            //if (!Directory.Exists(atlasDir))
            //    Directory.CreateDirectory(atlasDir);
            //File.WriteAllBytes(atlasPath, byteData); //we will reattach this in a bit
            //AssetDatabase.CreateAsset(supertexture, Path.Combine(basePath, $"{supertexture.name}.texture"));
            //supertexture = AssetDatabase.LoadAssetAtPath(Path.Combine(subdir, $"{supertexture.name}.anim"), typeof(Texture2D)) as Texture2D;

            for (var i = 0; i < rects.Length; i++)
            {
                var texrect = new Rect(rects[i].x * supertexture.width, rects[i].y * supertexture.height, rects[i].width * supertexture.width,
                    rects[i].height * supertexture.height);
                SpriteSizes.Add(new Vector2Int(Textures[i].width, Textures[i].height));
                var sprite = Sprite.Create(supertexture, texrect, new Vector2(0.5f, 0.5f), 50, 0, SpriteMeshType.FullRect);

                sprite.name = $"sprite_{basename}_{i:D4}";

                AssetDatabase.AddObjectToAsset(sprite, dataObject);

                Sprites.Add(sprite);
            }

            if (atlasPath.Contains("Shields") || atlasPath.Contains("status-curse"))
            {
                dataObject.ReverseSortingWhenFacingNorth = true;
                dataObject.IgnoreAnchor = true;
            }
            
            _br.Dispose();
            _ms.Dispose();
        }
    }
}