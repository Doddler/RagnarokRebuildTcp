using System.IO;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public interface IRoSprite
    {
        public struct BitmapSprite
        {
            public ushort ImageWidth;
            public ushort ImageHeight;
            public byte[] PaletteIndices;
        }
        
        public struct CompressedBitmapSprite
        {
            public ushort ImageWidth;
            public ushort ImageHeight;
            public ushort BufferSize;
            public byte[] CompressedPaletteIndices;
            public byte[] DecompressPaletteIndices()
            {
                // Debug.Log("Starting decompression");
                // Debug.Log($"Our target dimensions are: {ImageWidth}x{ImageHeight}");
                var decompressedPaletteIndices = new byte[ImageWidth * ImageHeight];
                // Debug.Log($"We have a total of {ImageWidth * ImageHeight} palette indices to get from {CompressedPaletteIndices.Length} compressed indices");
                var decompressedIndex = 0;
                var zeroRun = false;
                for (var compressedIndex = 0; compressedIndex < BufferSize; compressedIndex++)
                {
                    var currentByte = CompressedPaletteIndices[compressedIndex];
                    // Debug.Log($"Our current byte is {currentByte}");
                    // Debug.Log($"Our current decompressedIndex is {decompressedIndex}. The max size is {ImageWidth * ImageHeight}!");
                    switch (currentByte)
                    {
                        case 0 when !zeroRun:
                            // Debug.Log($"It's the start of a zero run!");
                            decompressedPaletteIndices[decompressedIndex++] = currentByte;
                            zeroRun = true;
                            break;
                        case 0 when zeroRun:
                            // Debug.Log($"Something is wrong with this file!");
                            throw new InvalidDataException("Found 00 00 while decompressing sprite. Not RLE?");
                        case > 0 when zeroRun:
                        {
                            // Debug.Log($"We are on a zero run!");
                            for (var count = 1; count == currentByte; count++)
                            {
                                // Debug.Log($"Adding zero number {count}!");
                                decompressedPaletteIndices[decompressedIndex++] = 0x00;
                            }
                            zeroRun = false;
                            break;
                        }
                        case > 0 when !zeroRun:
                            // Debug.Log($"It's not a zero run, and we found {currentByte}!");
                            decompressedPaletteIndices[decompressedIndex++] = currentByte;
                            break;
                    }
                }
                return decompressedPaletteIndices;
            }
        }
        public struct TrueColorSprite
        {
            public ushort ImageWidth;
            public ushort ImageHeight;
            public Color32[] PixelBuffer;
        }
        void ReadBytes(string filepath);
        void WriteBytes(string filepath);
    }
    class RoSpriteV20 : IRoSprite
    {
        public char[] Signature;
        public byte VersionMajor;
        public byte VersionMinor;
        public ushort BitmapImageCount;
        public ushort TrueColorImageCount;
        public IRoSprite.BitmapSprite[] BitmapSprites;
        public IRoSprite.TrueColorSprite[] TrueColorSprites;
        public Color32[] BitmapColors;
        
        /// <summary>
        /// Write spr data to file
        /// </summary>
        /// <param name="filePath"></param>
        public void WriteBytes(string filePath)
        {
            
        }

        public void ReadBytes(string filePath)
        {
            var binaryReader = new BinaryReader(new FileStream(filePath, FileMode.Open));
            
            Signature = binaryReader.ReadChars(2);
            VersionMajor = binaryReader.ReadByte();
            VersionMinor = binaryReader.ReadByte();
            BitmapImageCount = binaryReader.ReadUInt16();
            TrueColorImageCount = binaryReader.ReadUInt16();
            
            BitmapSprites = new IRoSprite.BitmapSprite[BitmapImageCount];
            for (var index = 0; index < BitmapSprites.Length; index++)
            {
                var bitmapSprite = new IRoSprite.BitmapSprite
                {
                    ImageWidth = binaryReader.ReadUInt16(),
                    ImageHeight = binaryReader.ReadUInt16()
                };
                bitmapSprite.PaletteIndices = new byte[bitmapSprite.ImageWidth * bitmapSprite.ImageHeight];
                bitmapSprite.PaletteIndices = binaryReader.ReadBytes(bitmapSprite.PaletteIndices.Length);
                BitmapSprites[index] = bitmapSprite;
            }
            
            TrueColorSprites = new IRoSprite.TrueColorSprite[TrueColorImageCount];
            for (var index = 0; index < TrueColorSprites.Length; index++)
            {
                var trueColorSprite = new IRoSprite.TrueColorSprite
                {
                    ImageWidth = binaryReader.ReadUInt16(),
                    ImageHeight = binaryReader.ReadUInt16()
                };
                trueColorSprite.PixelBuffer =  new Color32[trueColorSprite.ImageWidth * trueColorSprite.ImageHeight];
                for (var pixelIndex = 0; pixelIndex < trueColorSprite.PixelBuffer.Length; pixelIndex++)
                {
                    var trueColorPixel = new Color32()
                    {
                        a = binaryReader.ReadByte(),
                        b = binaryReader.ReadByte(),
                        g = binaryReader.ReadByte(),
                        r = binaryReader.ReadByte()
                    };
                    trueColorPixel.a = byte.MaxValue;
                    trueColorSprite.PixelBuffer[pixelIndex] = trueColorPixel;
                }
                TrueColorSprites[index] = trueColorSprite;
            }
            
            BitmapColors =  new Color32[256];
            for (var index = 0; index < BitmapColors.Length; index++)
            {
                var bitmapColor = new Color32()
                {
                    r = binaryReader.ReadByte(),
                    g = binaryReader.ReadByte(),
                    b = binaryReader.ReadByte(),
                    a = binaryReader.ReadByte()
                };
                bitmapColor.a = byte.MaxValue;
                BitmapColors[index] = bitmapColor;
            }
        }
    }
    
    class RoSpriteV21 : IRoSprite
    {
        public char[] Signature;
        public byte VersionMajor;
        public byte VersionMinor;
        public ushort BitmapImageCount;
        public ushort TrueColorImageCount;
        public IRoSprite.CompressedBitmapSprite[] CompressedBitmapSprites;
        public IRoSprite.TrueColorSprite[] TrueColorSprites;
        public Color32[] BitmapColors;
        public void WriteBytes(string filePath)
        {

        }
        public void ReadBytes(string filePath)
        {
            var binaryReader = new BinaryReader(new FileStream(filePath, FileMode.Open));
            
            Signature = binaryReader.ReadChars(2);
            VersionMajor = binaryReader.ReadByte();
            VersionMinor = binaryReader.ReadByte();
            BitmapImageCount = binaryReader.ReadUInt16();
            TrueColorImageCount = binaryReader.ReadUInt16();
            
            CompressedBitmapSprites = new IRoSprite.CompressedBitmapSprite[BitmapImageCount];
            for (var index = 0; index < CompressedBitmapSprites.Length; index++)
            {
                var bitmapSprite = new IRoSprite.CompressedBitmapSprite
                {
                    ImageWidth = binaryReader.ReadUInt16(),
                    ImageHeight = binaryReader.ReadUInt16(),
                    BufferSize = binaryReader.ReadUInt16()
                };
                bitmapSprite.CompressedPaletteIndices = new byte[bitmapSprite.BufferSize];
                bitmapSprite.CompressedPaletteIndices = binaryReader.ReadBytes(bitmapSprite.CompressedPaletteIndices.Length);
                CompressedBitmapSprites[index] = bitmapSprite;
            }
            
            TrueColorSprites = new IRoSprite.TrueColorSprite[TrueColorImageCount];
            for (var index = 0; index < TrueColorSprites.Length; index++)
            {
                var trueColorSprite = new IRoSprite.TrueColorSprite
                {
                    ImageWidth = binaryReader.ReadUInt16(),
                    ImageHeight = binaryReader.ReadUInt16()
                };
                trueColorSprite.PixelBuffer =  new Color32[trueColorSprite.ImageWidth * trueColorSprite.ImageHeight];
                for (var pixelIndex = 0; pixelIndex < trueColorSprite.PixelBuffer.Length; pixelIndex++)
                {
                    var trueColorPixel = new Color32
                    {
                        a = binaryReader.ReadByte(),
                        b = binaryReader.ReadByte(),
                        g = binaryReader.ReadByte(),
                        r = binaryReader.ReadByte()
                    };
                    trueColorPixel.a = byte.MaxValue;
                    trueColorSprite.PixelBuffer[pixelIndex] = trueColorPixel;
                }
                TrueColorSprites[index] = trueColorSprite;
            }
            
            BitmapColors =  new Color32[256];
            for (var index = 0; index < BitmapColors.Length; index++)
            {
                var bitmapColor = new Color32
                {
                    r = binaryReader.ReadByte(),
                    g = binaryReader.ReadByte(),
                    b = binaryReader.ReadByte(),
                    a = binaryReader.ReadByte(),
                };
                bitmapColor.a = byte.MaxValue;
                BitmapColors[index] = bitmapColor;
            }
        }
    }
}