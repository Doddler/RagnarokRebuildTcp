using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class RoSpr
    {
        private readonly struct Versions
        {
            public const string V20 = "2.0";
            public const string V21 = "2.1";
        }

        public string Version
        {
            get { return $"{VersionMajor}.{VersionMinor}"; }
        }
        public RoBitmapImage[] BitmapImages
        {
            get
            {
                var bitmapImages = Version switch
                {
                    Versions.V20 => roSprData.BitmapImages,
                    Versions.V21 => DecompressBitmapImage(roSprData.CompressedBitmapImages),
                    _ => throw new InvalidDataException("Invalid spr version?")
                };
                return bitmapImages;
            }
            set
            {
                switch (Version)
                {
                    case Versions.V20:
                    {
                        if (value.Length > ushort.MaxValue)
                        {
                            throw new FormatException($"BitMapImages cant have more than {ushort.MaxValue} entries");
                        }
                        roSprData.BitmapImages = value;
                        roSprData.BitmapImageCount = (ushort)value.Length;
                        break;
                    }
                    case Versions.V21:
                    {
                        if (value.Length > ushort.MaxValue)
                        {
                            throw new FormatException($"BitMapImages cant have more than {ushort.MaxValue} entries");
                        }
                        roSprData.CompressedBitmapImages = CompressBitmapImage(value);
                        roSprData.BitmapImageCount = (ushort)value.Length;
                        break;
                    }

                }
            }
        }
        public RoTrueColorImage[] TrueColorImages
        {
            get { return roSprData.TrueColorImages; }

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
            get { return roSprData.PaletteColors; }
            set
            {
                if (value.Length > 256)
                {
                    throw new FormatException("PaletteColors cant have more than 256 entries");
                }
                roSprData.PaletteColors = value;
            }
        }

        private char[] Signature
        {
            get { return roSprData.Signature; }
            set { roSprData.Signature = value; }
        }
        private byte VersionMajor
        {
            get { return roSprData.VersionMajor; }
            set { roSprData.VersionMajor = value; }
        }
        private byte VersionMinor
        {
            get { return roSprData.VersionMinor; }
            set { roSprData.VersionMinor = value; }
        }
        private ushort BitmapImageCount
        {
            get { return roSprData.BitmapImageCount; }
            set { roSprData.BitmapImageCount = value; }
        }
        private RoCompressedBitmapImage[] CompressedBitmapImages
        {
            get
            {
                var compressedBitmapImages = Version switch
                {
                    Versions.V21 => roSprData.CompressedBitmapImages,
                    Versions.V20 => throw new InvalidDataException("Compressed images are available on version 2.1"),
                    _ => throw new InvalidDataException("Invalid spr version?")
                };
                return compressedBitmapImages;
            }
            set
            {
                switch (Version)
                {
                    case Versions.V21:
                        if (value.Length > ushort.MaxValue)
                        {
                            throw new FormatException($"BitmapImages cant have more than {ushort.MaxValue} entries");
                        }
                        roSprData.CompressedBitmapImages = value;
                        roSprData.BitmapImageCount = (ushort)value.Length;
                        break;
                    case Versions.V20:
                        throw new InvalidDataException("Compressed images are available on version 2.1");
                    default:
                        throw new InvalidDataException("Invalid spr version?");
                }
            }
        }
        private ushort TrueColorImageCount
        {
            get { return roSprData.TrueColorImageCount; }
            set { roSprData.TrueColorImageCount = value; }
        }

        private RoSprData roSprData;

        /// <summary>
        /// Compress the given RoBitmapImage and return a RoCompressedBitmapImage 
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static RoCompressedBitmapImage CompressBitmapImage(RoBitmapImage image)
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
            var compressedImage = new RoCompressedBitmapImage
            {
                ImageHeight = image.ImageHeight,
                ImageWidth = image.ImageWidth,
                CompressedSize = (ushort)compressedPaletteIndexesList.Count,
                CompressedPaletteIndexes = compressedPaletteIndexesList.ToArray()
            };
            return compressedImage;
        }
        /// <summary>
        /// Compress the given RoBitmapImage array and return a RoCompressedBitmapImage array 
        /// </summary>
        /// <param name="images"></param>
        /// <returns></returns>
        public static RoCompressedBitmapImage[] CompressBitmapImage(RoBitmapImage[] images)
        {
            var compressedBitmapImages = new RoCompressedBitmapImage[images.Length];
            foreach (var ii in images.Select((image, index) => new { image, index }))
            {
                var compressedBitmapImage = CompressBitmapImage(ii.image);
                compressedBitmapImages[ii.index] = compressedBitmapImage;
            }
            return compressedBitmapImages;
        }
        public static RoBitmapImage DecompressBitmapImage(RoCompressedBitmapImage roCompressedImage)
        {
            var decompressedPaletteIndexes = new byte[roCompressedImage.ImageWidth * roCompressedImage.ImageHeight];
            var decompressedIndex = 0;
            var zeroRun = false;
            for (var compressedIndex = 0; compressedIndex < roCompressedImage.CompressedSize; compressedIndex++)
            {
                var currentByte = roCompressedImage.CompressedPaletteIndexes[compressedIndex];
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
            var bitmapImage = new RoBitmapImage()
            {
                ImageHeight = roCompressedImage.ImageHeight,
                ImageWidth = roCompressedImage.ImageWidth,
                PaletteIndexes = decompressedPaletteIndexes
            };
            return bitmapImage;
        }
        public static RoBitmapImage[] DecompressBitmapImage(RoCompressedBitmapImage[] compressedImages)
        {
            var bitmapImages = new RoBitmapImage[compressedImages.Length];
            foreach (var ii in compressedImages.Select((image, index) => new { image, index }))
            {
                var bitmapImage = DecompressBitmapImage(ii.image);
                bitmapImages[ii.index] = bitmapImage;
            }
            return bitmapImages;
        }

        public RoSpr(string filename)
        {
            ReadBytes(filename);
        }

        public RoSpr(FileStream filestream)
        {
            ReadBytes(filestream);
        }

        public RoSpr(BinaryReader binaryReader)
        {
            ReadBytes(binaryReader);
        }

        /// <summary>
        /// Write spr data to file
        /// </summary>
        /// <param name="filePath"></param>
        public void WriteBytes(string filePath)
        {
            var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var binaryWriter = new BinaryWriter(fileStream);

            binaryWriter.Write(Signature);
            binaryWriter.Write(VersionMinor);
            binaryWriter.Write(VersionMajor);
            binaryWriter.Write(BitmapImageCount);
            binaryWriter.Write(TrueColorImageCount);
            switch (Version)
            {
                case Versions.V20:
                {
                    foreach (var bitmapImage in BitmapImages)
                    {
                        binaryWriter.Write(bitmapImage.ImageWidth);
                        binaryWriter.Write(bitmapImage.ImageWidth);
                        binaryWriter.Write(bitmapImage.PaletteIndexes);
                    }
                    break;
                }
                case Versions.V21:
                {
                    foreach (var compressedBitmapImage in CompressedBitmapImages)
                    {
                        binaryWriter.Write(compressedBitmapImage.ImageWidth);
                        binaryWriter.Write(compressedBitmapImage.ImageHeight);
                        binaryWriter.Write(compressedBitmapImage.CompressedSize);
                        binaryWriter.Write(compressedBitmapImage.CompressedPaletteIndexes);
                    }
                    break;
                }
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
            fileStream.Close();
        }

        /// <summary>
        /// Read spr data from file
        /// </summary>
        /// <param name="filePath"></param>
        public void ReadBytes(string filePath)
        {
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            try
            {
                ReadBytes(fileStream);
            }
            catch (NotSupportedException)
            {
                fileStream.Close();
                throw;
            }
            fileStream.Close();
        }

        public void ReadBytes(FileStream fileStream)
        {
            var binaryReader = new BinaryReader(fileStream);
            try
            {
                ReadBytes(binaryReader);
            }
            catch (NotSupportedException)
            {
                binaryReader.Close();
                throw;
            }
            binaryReader.Close();
        }

        public void ReadBytes(BinaryReader binaryReader)
        {
            roSprData = new RoSprData();

            Signature = binaryReader.ReadChars(2);
            if (new string(Signature) != "SP")
            {
                throw new NotSupportedException("Not a spr file");
            }
            VersionMinor = binaryReader.ReadByte();
            VersionMajor = binaryReader.ReadByte();
            if (!new[] { "2.0", "2.1" }.Contains(Version))
            {
                throw new NotSupportedException("Unsupported spr version");
            }
            BitmapImageCount = binaryReader.ReadUInt16();
            TrueColorImageCount = binaryReader.ReadUInt16();

            switch (Version)
            {
                case Versions.V20:
                {
                    BitmapImages = new RoBitmapImage[BitmapImageCount];
                    for (var index = 0; index < BitmapImageCount; index++)
                    {
                        var bitmapSprite = new RoBitmapImage
                        {
                            ImageWidth = binaryReader.ReadUInt16(),
                            ImageHeight = binaryReader.ReadUInt16()
                        };
                        bitmapSprite.PaletteIndexes = new byte[bitmapSprite.ImageWidth * bitmapSprite.ImageHeight];
                        bitmapSprite.PaletteIndexes = binaryReader.ReadBytes(bitmapSprite.PaletteIndexes.Length);
                        BitmapImages[index] = bitmapSprite;
                    }
                    break;
                }
                case Versions.V21:
                {
                    CompressedBitmapImages = new RoCompressedBitmapImage[BitmapImageCount];
                    for (var index = 0; index < BitmapImageCount; index++)
                    {
                        var bitmapSprite = new RoCompressedBitmapImage
                        {
                            ImageWidth = binaryReader.ReadUInt16(),
                            ImageHeight = binaryReader.ReadUInt16(),
                            CompressedSize = binaryReader.ReadUInt16()
                        };
                        bitmapSprite.CompressedPaletteIndexes = new byte[bitmapSprite.CompressedSize];
                        bitmapSprite.CompressedPaletteIndexes = binaryReader.ReadBytes(bitmapSprite.CompressedPaletteIndexes.Length);
                        CompressedBitmapImages[index] = bitmapSprite;
                    }
                    break;
                }

            }


            TrueColorImages = new RoTrueColorImage[TrueColorImageCount];
            for (var index = 0; index < TrueColorImageCount; index++)
            {
                var trueColorSprite = new RoTrueColorImage
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
                TrueColorImages[index] = trueColorSprite;
            }

            PaletteColors = new Color32[256];
            for (var index = 0; index < 256; index++)
            {
                var bitmapColor = new Color32()
                {
                    r = binaryReader.ReadByte(),
                    g = binaryReader.ReadByte(),
                    b = binaryReader.ReadByte(),
                    a = binaryReader.ReadByte()
                };
                bitmapColor.a = byte.MaxValue;
                PaletteColors[index] = bitmapColor;
            }
        }
    }
}