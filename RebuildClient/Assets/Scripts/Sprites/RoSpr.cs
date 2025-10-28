using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class RoSpr<T> where T : IRoSpr
    {
        public char[] Signature
        {
            get
            {
                return roSprData.Signature;
            }
            set
            {
                roSprData.Signature = value;
            }
        }
        public byte VersionMajor
        {
            get
            {
                return roSprData.VersionMajor;
            }

            private set
            {
                roSprData.VersionMajor = value;
            }
        }
        public byte VersionMinor
        {
            get
            {
                return roSprData.VersionMinor;
            }

            private set
            {
                roSprData.VersionMinor = value;
            }
        }
        private ushort BitmapImageCount
        {
            get
            {
                return roSprData.BitmapImageCount;
            }

            set
            {
                roSprData.BitmapImageCount = value;
            }
        }
        public ushort TrueColorImageCount
        {
            get
            {
                return roSprData.TrueColorImageCount;
            }
            private set
            {
                roSprData.TrueColorImageCount = value;
            }
        }
        public BitmapImage[] BitmapImages
        {
            get
            {
                var images = roSprData switch
                {
                    RoSprV20 v20 => v20.BitmapImages,
                    RoSprV21 v21 => DecompressBitmapImage(v21.CompressedBitmapImages),
                    _ => throw new InvalidDataException("Invalid sprite version?")
                };
                return images;
            }
            set
            {
                switch (roSprData)
                {
                    case RoSprV20 v20:
                        if (value.Length > ushort.MaxValue)
                        {
                            throw new FormatException($"BitMapImages cant have more than {ushort.MaxValue} entries");
                        }
                        v20.BitmapImages = value;
                        v20.BitmapImageCount = (ushort)value.Length;
                        roSprData = (T)(object)v20;
                        break;
                    case RoSprV21 v21:
                        if (value.Length > ushort.MaxValue)
                        {
                            throw new FormatException($"BitMapImages cant have more than {ushort.MaxValue} entries");
                        }
                        v21.CompressedBitmapImages = CompressBitmapImage(value);
                        v21.BitmapImageCount = (ushort)value.Length;
                        roSprData = (T)(object)v21;
                        break;
                }
            }
        }
        public TrueColorImage[] TrueColorImages
        {
            get
            {
                return roSprData.TrueColorImages;
            }

            set
            {
                if (value.Length > ushort.MaxValue)
                {
                    throw new FormatException($"TrueColorImages cant have more than {ushort.MaxValue} entries");
                }
                roSprData.TrueColorImages = value;
                roSprData.TrueColorImageCount = (ushort)value.Length;
            }
        }
        public Color32[] PaletteColors
        {
            get
            {
                return roSprData.PaletteColors;
            }
            set
            {
                if (value.Length > 256)
                {
                    throw new FormatException("PaletteColors cant have more than 256 entries");
                }
                roSprData.PaletteColors = value;
            }
        }
        
        private T roSprData;
        
        /// <summary>
        /// Compress the given BitmapImage and return a CompressedBitmapImage 
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static CompressedBitmapImage CompressBitmapImage(BitmapImage image)
        {
            var compressedPaletteIndexesList = new List<byte>();
            var zeroRun = false;
            byte zeroCount = 0;
            foreach (var colorIndex in image.PaletteIndexes)
            {
                switch (colorIndex)
                {
                    case 0 when !zeroRun:
                        zeroRun = true;
                        zeroCount++;
                        compressedPaletteIndexesList.Add(colorIndex);
                        break;
                    case 0:
                        zeroCount++;
                        break;
                    case > 0 when !zeroRun:
                        compressedPaletteIndexesList.Add(colorIndex);
                        break;
                    case > 0:
                        zeroRun = false;
                        compressedPaletteIndexesList.Add(zeroCount);
                        zeroCount = 0;
                        compressedPaletteIndexesList.Add(colorIndex);
                        break;
                }
            }
            var compressedImage = new CompressedBitmapImage
            {
                ImageHeight = image.ImageHeight,
                ImageWidth = image.ImageWidth,
                CompressedSize = (ushort)compressedPaletteIndexesList.Count,
                CompressedPaletteIndexes = compressedPaletteIndexesList.ToArray()
            };
            return compressedImage;
        }
        /// <summary>
        /// Compress the given BitmapImage array and return a CompressedBitmapImage array 
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static CompressedBitmapImage[] CompressBitmapImage(BitmapImage[] images)
        {
            var compressedBitmapImages = new CompressedBitmapImage[images.Length];
            foreach (var ii in images.Select((image, index) => new { image, index }))
            {
                var compressedBitmapImage = CompressBitmapImage(ii.image);
                compressedBitmapImages[ii.index] = compressedBitmapImage;
            }
            return compressedBitmapImages;
        }
        public static BitmapImage DecompressBitmapImage(CompressedBitmapImage compressedImage)
        {
            var decompressedPaletteIndexes = new byte[compressedImage.ImageWidth * compressedImage.ImageHeight];
            var decompressedIndex = 0;
            var zeroRun = false;
            for (var compressedIndex = 0; compressedIndex < compressedImage.CompressedSize; compressedIndex++)
            {
                var currentByte = compressedImage.CompressedPaletteIndexes[compressedIndex];
                switch (currentByte)
                {
                    case 0 when !zeroRun:
                        zeroRun = true;
                        break;
                    case 0:
                        throw new InvalidDataException("Found 00 00 while decompressing sprite. Not RLE?");
                    case > 0 when zeroRun:
                    {
                        for (var count = 1; count <= currentByte; count++)
                        {
                            decompressedPaletteIndexes[decompressedIndex++] = 0x00;
                        }
                        zeroRun = false;
                        break;
                    }
                    case > 0:
                        decompressedPaletteIndexes[decompressedIndex++] = currentByte;
                        break;
                }
            }
            var bitmapImage = new BitmapImage()
            {
                ImageHeight = compressedImage.ImageHeight,
                ImageWidth = compressedImage.ImageWidth,
                PaletteIndexes = decompressedPaletteIndexes
            };
            return bitmapImage;
        }
        public static BitmapImage[] DecompressBitmapImage(CompressedBitmapImage[] compressedImages)
        {
            var bitmapImages = new BitmapImage[compressedImages.Length];
            foreach (var ii in compressedImages.Select((image, index) => new {image, index}))
            {
                var bitmapImage = DecompressBitmapImage(ii.image);
                bitmapImages[ii.index] = bitmapImage;
            }
            return bitmapImages;
        }
        
        /// <summary>
        /// Write spr data to file
        /// </summary>
        /// <param name="filePath"></param>
        public void WriteBytes(string filePath)
        {
            var binaryWriter = new BinaryWriter(new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite));
            
            binaryWriter.Write(Signature);
            binaryWriter.Write(VersionMajor);
            binaryWriter.Write(VersionMinor);
            binaryWriter.Write(BitmapImageCount);
            binaryWriter.Write(roSprData.TrueColorImageCount);
            switch (roSprData)
            {
                case RoSprV20 v20:
                    foreach (var bitmapImage in v20.BitmapImages)
                    {
                        binaryWriter.Write(bitmapImage.ImageWidth);
                        binaryWriter.Write(bitmapImage.ImageWidth);
                        binaryWriter.Write(bitmapImage.PaletteIndexes);
                    }
                    break;
                case RoSprV21 v21:
                    foreach (var compressedBitmapImage in v21.CompressedBitmapImages)
                    {
                        binaryWriter.Write(compressedBitmapImage.ImageWidth);
                        binaryWriter.Write(compressedBitmapImage.ImageHeight);
                        binaryWriter.Write(compressedBitmapImage.CompressedSize);
                        binaryWriter.Write(compressedBitmapImage.CompressedPaletteIndexes);
                    }
                    break;
            }
            foreach (var trueColorImage in TrueColorImages)
            {
                binaryWriter.Write(trueColorImage.ImageWidth);
                binaryWriter.Write(trueColorImage.ImageHeight);
                foreach (var color in trueColorImage.ImageData)
                {
                    binaryWriter.Write(color);       
                }
            }

            foreach (var color in PaletteColors)
            {
                binaryWriter.Write(color.r);
                binaryWriter.Write(color.g);
                binaryWriter.Write(color.b);
                binaryWriter.Write(color.a);
            }
            
            binaryWriter.Close();
        }

        /// <summary>
        /// Read spr data from file
        /// </summary>
        /// <param name="filePath"></param>
        public void ReadBytes(string filePath)
        {
            var binaryReader = new BinaryReader(new FileStream(filePath, FileMode.Open, FileAccess.Read));

            Signature = binaryReader.ReadChars(2);
            VersionMajor = binaryReader.ReadByte();
            VersionMinor = binaryReader.ReadByte();
            BitmapImageCount = binaryReader.ReadUInt16();
            TrueColorImageCount = binaryReader.ReadUInt16();

            switch (roSprData)
            {
                case RoSprV20 v20:
                    v20.BitmapImages = new BitmapImage[roSprData.BitmapImageCount];
                    for (var index = 0; index < v20.BitmapImages.Length; index++)
                    {
                        var bitmapSprite = new BitmapImage
                        {
                            ImageWidth = binaryReader.ReadUInt16(),
                            ImageHeight = binaryReader.ReadUInt16()
                        };
                        bitmapSprite.PaletteIndexes = new byte[bitmapSprite.ImageWidth * bitmapSprite.ImageHeight];
                        bitmapSprite.PaletteIndexes = binaryReader.ReadBytes(bitmapSprite.PaletteIndexes.Length);
                        v20.BitmapImages[index] = bitmapSprite;
                    }
                    roSprData = (T)(object)v20;
                    break;
                case RoSprV21 v21:
                    v21.CompressedBitmapImages = new CompressedBitmapImage[roSprData.BitmapImageCount];
                    for (var index = 0; index < v21.CompressedBitmapImages.Length; index++)
                    {
                        var bitmapSprite = new CompressedBitmapImage
                        {
                            ImageWidth = binaryReader.ReadUInt16(),
                            ImageHeight = binaryReader.ReadUInt16(),
                            CompressedSize = binaryReader.ReadUInt16()
                        };
                        bitmapSprite.CompressedPaletteIndexes = new byte[bitmapSprite.CompressedSize];
                        bitmapSprite.CompressedPaletteIndexes = binaryReader.ReadBytes(bitmapSprite.CompressedPaletteIndexes.Length);
                        v21.CompressedBitmapImages[index] = bitmapSprite;
                    }
                    roSprData = (T)(object)v21;
                    break;
            }


            roSprData.TrueColorImages = new TrueColorImage[roSprData.TrueColorImageCount];
            for (var index = 0; index < roSprData.TrueColorImages.Length; index++)
            {
                var trueColorSprite = new TrueColorImage
                {
                    ImageWidth = binaryReader.ReadUInt16(),
                    ImageHeight = binaryReader.ReadUInt16()
                };
                trueColorSprite.ImageData = new Color32[trueColorSprite.ImageWidth * trueColorSprite.ImageHeight];
                for (var pixelIndex = 0; pixelIndex < trueColorSprite.ImageData.Length; pixelIndex++)
                {
                    var trueColorPixel = new Color32()
                    {
                        a = binaryReader.ReadByte(),
                        b = binaryReader.ReadByte(),
                        g = binaryReader.ReadByte(),
                        r = binaryReader.ReadByte()
                    };
                    trueColorPixel.a = byte.MaxValue;
                    trueColorSprite.ImageData[pixelIndex] = trueColorPixel;
                }
                roSprData.TrueColorImages[index] = trueColorSprite;
            }

            roSprData.PaletteColors = new Color32[256];
            for (var index = 0; index < roSprData.PaletteColors.Length; index++)
            {
                var bitmapColor = new Color32()
                {
                    r = binaryReader.ReadByte(),
                    g = binaryReader.ReadByte(),
                    b = binaryReader.ReadByte(),
                    a = binaryReader.ReadByte()
                };
                bitmapColor.a = byte.MaxValue;
                roSprData.PaletteColors[index] = bitmapColor;
            }
            
            binaryReader.Close();
        }
    }
}