using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scripts.Sprites
{
    public class RoSprAsset : ScriptableObject
    {
        public int InstanceID
        {
            get
            {
                if (instanceID == 0)
                    instanceID = GetInstanceID();
                return instanceID;
            }
        }
        public int HashCode
        {
            get
            {
                return InstanceID.GetHashCode();
            }
        }
        
        [HideInInspector] public string spriteVersion;
        [HideInInspector] public string filepath;
        [HideInInspector] public string sprFileName;
        [HideInInspector] public Texture2D palette;
        [HideInInspector] public Texture2D atlas;
        [HideInInspector] public Rect[] atlasRects;
        
        private int instanceID;
        private int hashCode;
        
        public void Load(string assetFilePath)
        {
            filepath = assetFilePath;
            sprFileName = Path.GetFileNameWithoutExtension(filepath);
            atlas = new Texture2D(2,2,TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                alphaIsTransparency = true,
                name = $"{sprFileName}_atlas"
            };

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
            reader.Dispose();
            stream.Dispose();
            switch (version)
            {
                case 20:
                    Load<RoSprV20>();
                    break;
                case 21:
                    Load<RoSprV21>();
                    break;
            }
        }
        
        private void Load<T>() where T : IRoSpr
        {
            
            var rawSprData = new RoSpr<T>();
            rawSprData.ReadBytes(filepath);
            
            spriteVersion = $"{rawSprData.VersionMajor}.{rawSprData.VersionMinor}";
            
            var palTex = new Texture2D(16,16, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                alphaIsTransparency = false,
                name = $"{sprFileName}_palette"
            };
            foreach (var ci in rawSprData.PaletteColors.Select((color, index) => new {color, index}))
            {
                var pColor = rawSprData.PaletteColors[ci.index];
                palTex.SetPixel(
                    ci.index % 16,
                    15 - ci.index / 16,
                    pColor
                );
            }
            palTex.Apply();
            palette = palTex;
            
            var sprites = new List<Texture2D>();
            foreach (var bitMapSprite in rawSprData.BitmapImages)
            {
                var texture = new Texture2D(bitMapSprite.ImageWidth, bitMapSprite.ImageHeight, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    alphaIsTransparency = false
                };
                foreach (var cii in bitMapSprite.PaletteIndexes.Select((colorIndex, index) => new {colorIndex, index}))
                {
                    var pColor = cii.colorIndex == 0 ? new Color32(0,0,0,0) : rawSprData.PaletteColors[cii.colorIndex];
                    texture.SetPixel(
                        cii.index % bitMapSprite.ImageWidth, 
                        bitMapSprite.ImageHeight - cii.index / bitMapSprite.ImageWidth,
                        pColor
                    );
                }
                texture.Apply();
                sprites.Add(texture);
            }
            atlasRects = atlas.PackTextures(sprites.ToArray(), 0);
            sprites.Clear();
        }
    }
}