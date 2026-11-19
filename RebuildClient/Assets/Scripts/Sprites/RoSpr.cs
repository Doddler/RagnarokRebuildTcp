using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class RoSpr
    {
        private const int PaletteColorCount = 256;
        private const string ExpectedSignature = "SP";

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
                if (value.Length > ushort.MaxValue)
                {
                    throw new FormatException($"BitMapImages cant have more than {ushort.MaxValue} entries");
                }

                switch (Version)
                {
                    case Versions.V20:
                        roSprData.BitmapImages = value;
                        break;
                    case Versions.V21:
                        roSprData.CompressedBitmapImages = CompressBitmapImage(value);
                        break;
                    default:
                        throw new InvalidDataException("Invalid SPR version?");
                }
                roSprData.BitmapImageCount = (ushort)value.Length;
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
                if (value.Length > PaletteColorCount)
                {
                    throw new FormatException($"PaletteColors cant have more than {PaletteColorCount} entries");
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

        public static RoCompressedBitmapImage CompressBitmapImage(RoBitmapImage image)
        {
            var bytes = new List<byte>();
            for (var i = 0; i < image.PaletteIndexes.Length;)
            {
                var value = image.PaletteIndexes[i++];
                bytes.Add(value);
                if (value != 0) continue;
                var count = 1;
                while (i < image.PaletteIndexes.Length && image.PaletteIndexes[i] == 0 && count < byte.MaxValue)
                {
                    i++;
                    count++;
                }
                bytes.Add((byte)count);
            }
            if (bytes.Count > ushort.MaxValue)
                throw new InvalidDataException("Compressed image exceeds the SPR size limit.");
            return new RoCompressedBitmapImage
            {
                ImageWidth = image.ImageWidth,
                ImageHeight = image.ImageHeight,
                CompressedSize = (ushort)bytes.Count,
                CompressedPaletteIndexes = bytes.ToArray()
            };
        }
        public static RoCompressedBitmapImage[] CompressBitmapImage(RoBitmapImage[] images)
        {
            return Array.ConvertAll(images, CompressBitmapImage);
        }
        public static RoBitmapImage DecompressBitmapImage(RoCompressedBitmapImage roCompressedImage)
        {
            var pixelCount = checked(roCompressedImage.ImageWidth * roCompressedImage.ImageHeight);
            var source = roCompressedImage.CompressedPaletteIndexes;
            var output = 0;
            if (source.Length != roCompressedImage.CompressedSize)
                throw new InvalidDataException("Truncated compressed image.");
            // A byte pair encodes at most 255 indexed pixels.
            if (pixelCount > source.Length / 2 * byte.MaxValue + source.Length % 2)
                throw new InvalidDataException("Compressed image cannot fill its declared dimensions.");
            var pixels = new byte[pixelCount];
            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
                var count = 1;
                if (value == 0)
                {
                    if (++i == source.Length) throw new InvalidDataException("Missing zero-run length.");
                    count = source[i];
                    // Legacy encoders use 00 00 for two literal zero pixels.
                    if (count == 0) count = 2;
                }
                if (output + count > pixels.Length) throw new InvalidDataException("Image run exceeds dimensions.");
                for (var j = 0; j < count; j++) pixels[output++] = value;
            }
            if (output != pixels.Length) throw new InvalidDataException("Incomplete image data.");
            return new RoBitmapImage
            {
                ImageWidth = roCompressedImage.ImageWidth,
                ImageHeight = roCompressedImage.ImageHeight,
                PaletteIndexes = pixels
            };
        }
        public static RoBitmapImage[] DecompressBitmapImage(RoCompressedBitmapImage[] compressedImages)
        {
            return Array.ConvertAll(compressedImages, DecompressBitmapImage);
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

        public void ReadBytes(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            ReadBytes(stream);
        }

        public void ReadBytes(FileStream fileStream)
        {
            using var reader = new BinaryReader(fileStream, System.Text.Encoding.UTF8, true);
            ReadBytes(reader);
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            EnsureRemainingBytes(reader, count);
            var bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException("Truncated SPR image.");
            return bytes;
        }

        private static void EnsureRemainingBytes(BinaryReader reader, long count)
        {
            var stream = reader.BaseStream;
            if (stream.CanSeek && count > stream.Length - stream.Position)
                throw new EndOfStreamException("Truncated SPR image.");
        }

        public void ReadBytes(BinaryReader binaryReader)
        {
            roSprData = new RoSprData();

            ReadHeader(binaryReader);

            switch (Version)
            {
                case Versions.V20:
                    {
                        ReadBitmapImages(binaryReader);
                        break;
                    }
                case Versions.V21:
                    {
                        ReadCompressedBitmapImages(binaryReader);
                        break;
                    }

            }

            ReadTrueColorImages(binaryReader);
            ReadPalette(binaryReader);
        }

        private void ReadHeader(BinaryReader binaryReader)
        {
            Signature = binaryReader.ReadChars(ExpectedSignature.Length);
            if (new string(Signature) != ExpectedSignature)
            {
                throw new NotSupportedException("Not a spr file");
            }

            VersionMinor = binaryReader.ReadByte();
            VersionMajor = binaryReader.ReadByte();
            if (Version != Versions.V20 && Version != Versions.V21)
            {
                throw new NotSupportedException("Unsupported spr version");
            }

            BitmapImageCount = binaryReader.ReadUInt16();
            TrueColorImageCount = binaryReader.ReadUInt16();
        }

        private void ReadTrueColorImages(BinaryReader binaryReader)
        {
            TrueColorImages = new RoTrueColorImage[TrueColorImageCount];
            for (var index = 0; index < TrueColorImageCount; index++)
            {
                var trueColorSprite = new RoTrueColorImage
                {
                    ImageWidth = binaryReader.ReadUInt16(),
                    ImageHeight = binaryReader.ReadUInt16()
                };
                var pixelCount = checked(trueColorSprite.ImageWidth * trueColorSprite.ImageHeight);
                const int BytesPerTrueColorPixel = 4;
                EnsureRemainingBytes(binaryReader, (long)pixelCount * BytesPerTrueColorPixel);
                trueColorSprite.ImageData = new Color32[pixelCount];
                for (var pixelIndex = 0; pixelIndex < trueColorSprite.ImageData.Length; pixelIndex++)
                {
                    var trueColorPixel = new Color32()
                    {
                        a = binaryReader.ReadByte(),
                        b = binaryReader.ReadByte(),
                        g = binaryReader.ReadByte(),
                        r = binaryReader.ReadByte()
                    };
                    trueColorSprite.ImageData[pixelIndex] = trueColorPixel;
                }
                TrueColorImages[index] = trueColorSprite;
            }
        }

        private void ReadPalette(BinaryReader binaryReader)
        {
            PaletteColors = new Color32[PaletteColorCount];
            for (var index = 0; index < PaletteColorCount; index++)
            {
                var bitmapColor = new Color32()
                {
                    r = binaryReader.ReadByte(),
                    g = binaryReader.ReadByte(),
                    b = binaryReader.ReadByte(),
                    a = binaryReader.ReadByte()
                };
                PaletteColors[index] = bitmapColor;
            }
        }

        private void ReadCompressedBitmapImages(BinaryReader binaryReader)
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
                bitmapSprite.CompressedPaletteIndexes = ReadExactly(binaryReader, bitmapSprite.CompressedSize);
                CompressedBitmapImages[index] = bitmapSprite;
            }
        }

        private void ReadBitmapImages(BinaryReader binaryReader)
        {
            BitmapImages = new RoBitmapImage[BitmapImageCount];
            for (var index = 0; index < BitmapImageCount; index++)
            {
                var bitmapSprite = new RoBitmapImage
                {
                    ImageWidth = binaryReader.ReadUInt16(),
                    ImageHeight = binaryReader.ReadUInt16()
                };
                var pixelCount = checked(bitmapSprite.ImageWidth * bitmapSprite.ImageHeight);
                bitmapSprite.PaletteIndexes = ReadExactly(binaryReader, pixelCount);
                BitmapImages[index] = bitmapSprite;
            }
        }

    }
}