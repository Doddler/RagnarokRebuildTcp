using System.Runtime.InteropServices;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public interface IRoSpr
    {
        char[] Signature { get; set; }
        byte VersionMajor { get; set; }
        byte VersionMinor { get; set; }
        ushort BitmapImageCount { get; set; }
        ushort TrueColorImageCount { get; set; }
        TrueColorImage[] TrueColorImages { get; set; }
        Color32[] PaletteColors { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BitmapImage
    {
        public ushort ImageWidth;
        public ushort ImageHeight;
        public byte[] PaletteIndexes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CompressedBitmapImage
    {
        public ushort ImageWidth;
        public ushort ImageHeight;
        public ushort CompressedSize;
        public byte[] CompressedPaletteIndexes;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TrueColorImage
    {
        public ushort ImageWidth;
        public ushort ImageHeight;
        public Color32[] ImageData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RoSprV20 : IRoSpr
    {
        public char[] Signature { get; set; }
        public byte VersionMajor { get; set; }
        public byte VersionMinor { get; set; }
        public ushort BitmapImageCount { get; set; }
        public ushort TrueColorImageCount { get; set; }
        public BitmapImage[] BitmapImages { get; set; }
        public TrueColorImage[] TrueColorImages { get; set; }
        public Color32[] PaletteColors { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RoSprV21 : IRoSpr
    {
        public char[] Signature { get; set; }
        public byte VersionMajor { get; set; }
        public byte VersionMinor { get; set; }
        public ushort BitmapImageCount { get; set; }
        public ushort TrueColorImageCount { get; set; }
        public CompressedBitmapImage[] CompressedBitmapImages { get; set; }
        public TrueColorImage[] TrueColorImages { get; set; }
        public Color32[] PaletteColors { get; set; }
    }
}