using System.Runtime.InteropServices;
using Assets.Scripts;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Sprites
{
    public class RoSpriteV20
    {
        public struct BitmapSprite
        {
            public ushort ImageWidth;
            public ushort ImageHeight;
            public byte[] PaletteIndices;
        }

        public struct TrueColorPixel
        {
            public byte Alpha;
            public byte Blue;
            public byte Green;
            public byte Red;
        }

        public struct TrueColorSprite
        {
            public ushort ImageWidth;
            public ushort ImageHeight;
            public TrueColorPixel[] PixelBuffer;
        }

        public struct PaletteColor
        {
            public byte Red;
            public byte Green;
            public byte Blue;
            public byte Alpha;
        }

        public char[] Signature;
        public byte VersionMajor;
        public byte VersionMinor;
        public ushort BitmapImageCount;
        public ushort TrueColorImageCount;
        public BitmapSprite[] BitmapSprites;
        public TrueColorSprite[] TrueColorSprites;
        public PaletteColor[] BitmapColors;
    }
    
    public class RoSpriteV21
    {
        public struct BitmapSprite
        {
            public ushort ImageWidth;
            public ushort ImageHeight;
            public ushort BufferSize;
            public byte[] PaletteIndices;
        }

        public struct TrueColorPixel
        {
            public byte Alpha;
            public byte Blue;
            public byte Green;
            public byte Red;
        }

        public struct TrueColorSprite
        {
            public ushort ImageWidth;
            public ushort ImageHeight;
            public TrueColorPixel[] PixelBuffer;
        }

        public struct PaletteColor
        {
            public byte Red;
            public byte Green;
            public byte Blue;
            public byte Alpha;
        }

        public char[] Signature;
        public byte VersionMajor;
        public byte VersionMinor;
        public ushort BitmapImageCount;
        public ushort TrueColorImageCount;
        public BitmapSprite[] BitmapSprites;
        public TrueColorSprite[] TrueColorSprites;
        public PaletteColor[] BitmapColors;
    }
    
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
        public int HashCode => InstanceID.GetHashCode();

        [SerializeField] private new string name;
        [SerializeField] private Texture2D sprite;
        [SerializeField] private Texture2D palette;

    }

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