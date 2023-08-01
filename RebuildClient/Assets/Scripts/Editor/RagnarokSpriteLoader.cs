using System;
using System.Collections.Generic;
using System.IO;

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
        private int indexCount;
        private int rgbaCount;

        private List<SpriteFrameData> spriteFrames;
        private byte[] paletteData;

        public List<Texture2D> Textures = new List<Texture2D>();
        public List<Sprite> Sprites = new List<Sprite>();
        public List<Vector2Int> SpriteSizes = new List<Vector2Int>();

        public Texture2D Atlas;

        public int IndexCount => indexCount;
        
        private void ReadIndexedImage()
        {
            for (var i = 0; i < indexCount; i++)
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
            for (var i = 0; i < indexCount; i++)
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

                    var r = paletteData[index1 + 0];
                    var g = paletteData[index1 + 1];
                    var b = paletteData[index1 + 2];
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
            paletteData = br.ReadBytes(1024);
        }

        public void Load(UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            var filename = ctx.assetPath;
            var basename = Path.GetFileNameWithoutExtension(filename);

            var bytes = File.ReadAllBytes(filename);
            ms = new MemoryStream(bytes);
            br = new BinaryReader(ms);

            //fs = new FileStream(filename, FileMode.Open);
            //br = new BinaryReader(fs);

            var header = new string(br.ReadChars(2));
            if (header != "SP")
                throw new Exception("Not sprite");

            var minorVersion = br.ReadByte();
            var majorVersion = br.ReadByte();
            version = majorVersion * 10 + minorVersion;

            indexCount = br.ReadUInt16();
            rgbaCount = 0;
            
            if (version > 11)
                rgbaCount = br.ReadUInt16();

            //Debug.Log($"RGBA count: {rgbaCount}");

            var frameCount = indexCount + rgbaCount;
            var rgbaIndex = indexCount;

            spriteFrames = new List<SpriteFrameData>(frameCount);

            if (version < 21)
                ReadIndexedImage();
            else
                ReadRleIndexedImage();

            ReadRgbaImage();

            if(version > 10)
				ReadPalette();

            for (var i = 0; i < spriteFrames.Count; i++)
            {
	            Texture2D image;
                if(spriteFrames[i].IsIndexed)
					image = IndexedToTexture(spriteFrames[i]);
                else
					image = RgbaToTexture(spriteFrames[i]);
				image.name = $"{basename}_{i:D4}";
                image.hideFlags = HideFlags.HideInHierarchy;
                
                //ctx.AddObjectToAsset(image.name, image);

                //var sprite = Sprite.Create(image, new Rect(0, 0, indexedFrames[i].Width, indexedFrames[i].Height),
                //    new Vector2(0.5f, 0.5f), 100);

                //sprite.name = $"sprite_{basename}_{i:D4}";

                //ctx.AddObjectToAsset(sprite.name, sprite);
                
                Textures.Add(image);
                //Sprites.Add(sprite);
            }

            var basePath = Path.GetDirectoryName(ctx.assetPath);
            //var subdir = Path.Combine(basePath, "Poring");

            //if (!Directory.Exists(subdir))
            //    Directory.CreateDirectory(subdir);

            var supertexture = new Texture2D(2, 2);
            supertexture.name = $"{basename}_atlas";
            var rects = supertexture.PackTextures(Textures.ToArray(), 2, 2048, false);
            supertexture.filterMode = FilterMode.Bilinear;
            
            ctx.AddObjectToAsset(supertexture.name, supertexture);


            //var byteData = supertexture.EncodeToPNG();
            //File.WriteAllBytes(Path.Combine(basePath, supertexture.name + ".png"), byteData);
            //AssetDatabase.CreateAsset(supertexture, Path.Combine(basePath, $"{supertexture.name}.texture"));
            //supertexture = AssetDatabase.LoadAssetAtPath(Path.Combine(subdir, $"{supertexture.name}.anim"), typeof(Texture2D)) as Texture2D;

            

            Atlas = supertexture;

            for (var i = 0; i < rects.Length; i++)
            {
                //Debug.Log(rects[i]);

                var texrect = new Rect(rects[i].x * supertexture.width, rects[i].y * supertexture.height, rects[i].width * supertexture.width, rects[i].height * supertexture.height);
                
                //var xpivot = Mathf.CeilToInt(Textures[i].width / 2f) / (float)Textures[i].width;
                //var ypivot = Mathf.CeilToInt(Textures[i].height / 2f) / (float)Textures[i].height;
                //Debug.Log($"{Textures[i].width} {Textures[i].height} {xpivot} {ypivot}");



                SpriteSizes.Add(new Vector2Int(Textures[i].width, Textures[i].height));
                var sprite = Sprite.Create(supertexture, texrect, new Vector2(0.5f, 0.5f), 50, 0, SpriteMeshType.FullRect);
                
                sprite.name = $"sprite_{basename}_{i:D4}";
                


                //AssetDatabase.CreateAsset(sprite, Path.Combine(subdir, $"{sprite.name}.anim"));
                //sprite = AssetDatabase.LoadAssetAtPath(Path.Combine(subdir, $"{sprite.name}.anim"), typeof(Sprite)) as Sprite;
                ctx.AddObjectToAsset(sprite.name, sprite);

                Sprites.Add(sprite);
            }

            //var anim = new AnimationClip();
            //anim.frameRate = 60;
            //anim.name = "anim";
            //anim.wrapMode = WrapMode.ClampForever;
            
            //var spriteBinding = new EditorCurveBinding();
            //spriteBinding.type = typeof(SpriteRenderer);
            //spriteBinding.path = "";
            //spriteBinding.propertyName = "m_Sprite";

            //var keyframes = new ObjectReferenceKeyframe[Sprites.Count];

            //for (var i = 0; i < Sprites.Count; i++)
            //{
            //    keyframes[i] = new ObjectReferenceKeyframe();
            //    keyframes[i].time = i * 0.15f;
            //    keyframes[i].value = Sprites[i];
            //}


            //AnimationUtility.SetObjectReferenceCurve(anim, spriteBinding, keyframes);

            //ctx.AddObjectToAsset("anim", anim);
            
            br.Dispose();
            ms.Dispose();
        }
    }
}
