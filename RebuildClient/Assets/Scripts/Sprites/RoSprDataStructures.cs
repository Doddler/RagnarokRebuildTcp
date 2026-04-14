using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class RoSprData
    {
        public char[] Signature { get; set; }
        public byte VersionMinor { get; set; }
        public byte VersionMajor { get; set; }
        public ushort BitmapImageCount { get; set; }
        public ushort TrueColorImageCount { get; set; }
        public RoBitmapImage[] BitmapImages { get; set; }
        public RoCompressedBitmapImage[] CompressedBitmapImages { get; set; }
        public RoTrueColorImage[] TrueColorImages { get; set; }
        public Color32[] PaletteColors { get; set; }
    }

    public class RoBitmapImage
    {
        public ushort ImageWidth;
        public ushort ImageHeight;
        public byte[] PaletteIndexes;
    }

    public class RoCompressedBitmapImage
    {
        public ushort ImageWidth;
        public ushort ImageHeight;
        public ushort CompressedSize;
        public byte[] CompressedPaletteIndexes;
    }

    public class RoTrueColorImage
    {
        public ushort ImageWidth;
        public ushort ImageHeight;
        public Color32[] ImageData;
    }
}