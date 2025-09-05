using System.IO;

namespace Assets.Scripts.Sprites
{
    interface IRoSprite
    {
        void ReadBytes(string filepath);
        void WriteBytes(string filepath);
    }
    class RoSpriteV20 : IRoSprite
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
        
        /// <summary>
        /// Write spr data to file
        /// </summary>
        /// <param name="filePath"></param>
        public void WriteBytes(string filePath)
        {
            
        }

        public void ReadBytes(string filePath)
        {
            BinaryReader binaryReader = new BinaryReader(new FileStream(filePath, FileMode.Open));
            
            Signature = binaryReader.ReadChars(2);
            VersionMajor = binaryReader.ReadByte();
            VersionMinor = binaryReader.ReadByte();
            BitmapImageCount = binaryReader.ReadUInt16();
            TrueColorImageCount = binaryReader.ReadUInt16();
            
            BitmapSprites = new BitmapSprite[BitmapImageCount];
            for (var index = 0; index < BitmapSprites.Length; index++)
            {
                var bitmapSprite = new BitmapSprite();
                bitmapSprite.ImageWidth = binaryReader.ReadUInt16();
                bitmapSprite.ImageHeight = binaryReader.ReadUInt16();
                bitmapSprite.PaletteIndices = new byte[bitmapSprite.ImageWidth * bitmapSprite.ImageHeight];
                bitmapSprite.PaletteIndices = binaryReader.ReadBytes(bitmapSprite.PaletteIndices.Length);
                BitmapSprites[index] = bitmapSprite;
            }
            
            TrueColorSprites = new TrueColorSprite[TrueColorImageCount];
            for (var index = 0; index < TrueColorSprites.Length; index++)
            {
                var trueColorSprite = new TrueColorSprite();
                trueColorSprite.ImageWidth = binaryReader.ReadUInt16();
                trueColorSprite.ImageHeight = binaryReader.ReadUInt16();
                trueColorSprite.PixelBuffer =  new TrueColorPixel[trueColorSprite.ImageWidth * trueColorSprite.ImageHeight];
                for (var pixelIndex = 0; pixelIndex < trueColorSprite.PixelBuffer.Length; pixelIndex++)
                {
                    TrueColorPixel trueColorPixel = new TrueColorPixel();
                    trueColorPixel.Alpha = binaryReader.ReadByte();
                    trueColorPixel.Blue = binaryReader.ReadByte();
                    trueColorPixel.Green = binaryReader.ReadByte();
                    trueColorPixel.Red = binaryReader.ReadByte();
                    trueColorSprite.PixelBuffer[pixelIndex] = trueColorPixel;
                }
                TrueColorSprites[index] = trueColorSprite;
            }
            
            BitmapColors =  new PaletteColor[256];
            for (var index = 0; index < BitmapColors.Length; index++)
            {
                var bitmapColor = new PaletteColor();
                bitmapColor.Red = binaryReader.ReadByte();
                bitmapColor.Green = binaryReader.ReadByte();
                bitmapColor.Blue = binaryReader.ReadByte();
                bitmapColor.Alpha = binaryReader.ReadByte();
                BitmapColors[index] = bitmapColor;
            }
        }
    }
    
    class RoSpriteV21 : IRoSprite
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