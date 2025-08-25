namespace Sprites
{
    partial class RoSpriteV21
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
        public void WriteBytes(string filePath)
        {

        }
        public void ReadBytes(string filePath)
        {

        }
    }
}