using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class RoSprAsset : ScriptableObject
    {
        private int instanceID;
        public int InstanceID
        {
            get
            {
                if (instanceID == 0)
                    instanceID = GetInstanceID();
                return instanceID;
            }
        }
        private int hashCode;
        public int HashCode
        {
            get
            {
                return InstanceID.GetHashCode();
            }
        }

        private IRoSprite rawSpriteData;
        // [HideInInspector]
        public int spriteVersion;
        public string filepath;
        public string sprName;
        public List<Texture2D> sprites;
        public Texture2D palette;

        public void Load(string assetFilePath)
        {
            sprites =  new List<Texture2D>();
            filepath =  assetFilePath;
            sprName = Path.GetFileNameWithoutExtension(filepath);
            var stream = new MemoryStream(File.ReadAllBytes(filepath));
            var reader = new BinaryReader(stream);
            var header = new string(reader.ReadChars(2));
            if (header != "SP")
            {
                reader.Dispose();
                stream.Dispose();
                throw new Exception("Not sprite");
            }
            var minorVersion = reader.ReadByte();
            var majorVersion = reader.ReadByte();
            var version = majorVersion * 10 + minorVersion;
            Debug.Log($"Version: {version}");
            reader.Dispose();
            stream.Dispose();
            switch (version)
            {
                case 20:
                    spriteVersion = 20;
                    LoadV20();
                    break;
                case 21:
                    spriteVersion = 21;
                    LoadV21();
                    break;
            }
        }
        private void LoadV20()
        {
            rawSpriteData = new RoSpriteV20();
            Debug.Log("Reading V20 Bytes");
            rawSpriteData.ReadBytes(filepath);
            
            var palTex = new Texture2D(16,16, TextureFormat.RGBA32, false);
            palTex.SetPixels32(((RoSpriteV20)rawSpriteData).BitmapColors);
            palTex.Apply();
            palette = palTex;
            
            foreach (var bitMapSprite in ((RoSpriteV20)rawSpriteData).BitmapSprites)
            {
                var texture =  new Texture2D(bitMapSprite.ImageWidth,  bitMapSprite.ImageHeight, TextureFormat.RGBA32, false);
                foreach (var index in bitMapSprite.PaletteIndices)
                {
                    var pColor = ((RoSpriteV20)rawSpriteData).BitmapColors[index];
                    texture.SetPixel(
                        index % bitMapSprite.ImageWidth, 
                        index / bitMapSprite.ImageWidth,
                        pColor
                    );
                }
                texture.Apply();
                sprites.Add(texture);
            }
        }
        
        private void LoadV21()
        {
            rawSpriteData = new RoSpriteV21();
            Debug.Log("Reading V21 Bytes");
            rawSpriteData.ReadBytes(filepath);
            
            var palTex = new Texture2D(16,16, TextureFormat.RGBA32, false);
            palTex.SetPixels32(((RoSpriteV21)rawSpriteData).BitmapColors);
            palTex.Apply();
            palette = palTex;
            
            foreach (var compressedBitmapSprite in ((RoSpriteV21)rawSpriteData).CompressedBitmapSprites)
            {
                var texture = new Texture2D(compressedBitmapSprite.ImageWidth, compressedBitmapSprite.ImageHeight, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.None
                };
                
                var uncompressedPaletteIndices = compressedBitmapSprite.DecompressPaletteIndices();
                foreach (var index in uncompressedPaletteIndices)
                {
                    var pColor = ((RoSpriteV21)rawSpriteData).BitmapColors[index];
                    texture.SetPixel(
                        index % compressedBitmapSprite.ImageWidth, 
                        index / compressedBitmapSprite.ImageWidth,
                        pColor
                    );
                }
                texture.Apply();
                //Debug.Log($"Our beautiful Texture: {texture}");
                sprites.Add(texture);
            }
        }
    }
}