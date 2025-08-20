#region License and Information
/*****
*
* BMPLoader.cs
* 
* This is a simple implementation of a BMP file loader for Unity3D.
* Formats it should support are:
*  - 1 bit monochrome indexed
*  - 2-8 bit indexed
*  - 16 / 24 / 32 bit color (including "BI_BITFIELDS")
*  - RLE-4 and RLE-8 support has been added.
* 
* Unless the type is "BI_ALPHABITFIELDS" the loader does not interpret alpha
* values by default, however you can set the "ReadPaletteAlpha" setting to
* true to interpret the 4th (usually "00") value as alpha for indexed images.
* You can also set "ForceAlphaReadWhenPossible" to true so it will interpret
* the "left over" bits as alpha if there are any. It will also force to read
* alpha from a palette if it's an indexed image, just like "ReadPaletteAlpha".
* 
* It's not tested well to the bone, so there might be some errors somewhere.
* However I tested it with 4 different images created with MS Paint
* (1bit, 4bit, 8bit, 24bit) as those are the only formats supported.
* 
* 2017.02.05 - first version 
* 2017.03.06 - Added RLE4 / RLE8 support
* 
* Copyright (c) 2017 Markus Göbel (Bunny83)
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to
* deal in the Software without restriction, including without limitation the
* rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
* sell copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
* FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
* IN THE SOFTWARE.
* 
*****/
#endregion License and Information

using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace B83.Image.BMP
{
    public enum BMPComressionMode : int
    {
        BI_RGB = 0x00,
        BI_RLE8 = 0x01,
        BI_RLE4 = 0x02,
        BI_BITFIELDS = 0x03,
        BI_JPEG = 0x04,
        BI_PNG = 0x05,
        BI_ALPHABITFIELDS = 0x06,

        BI_CMYK = 0x0B,
        BI_CMYKRLE8 = 0x0C,
        BI_CMYKRLE4 = 0x0D,

    }
    public struct BMPFileHeader
    {
        public ushort magic; // "BM"
        public uint filesize;
        public uint reserved;
        public uint offset;
    }
    public struct BitmapInfoHeader
    {
        public uint size;
        public int width;
        public int height;
        public ushort nColorPlanes; // always 1
        public ushort nBitsPerPixel; // [1,4,8,16,24,32]
        public BMPComressionMode compressionMethod;
        public uint rawImageSize; // can be "0"
        public int xPPM;
        public int yPPM;
        public uint nPaletteColors;
        public uint nImportantColors;

        public int absWidth { get { return Mathf.Abs(width); } }
        public int absHeight { get { return Mathf.Abs(height); } }

    }

    public class BMPImage
    {
        public BMPFileHeader header;
        public BitmapInfoHeader info;
        public uint rMask = 0x00FF0000;
        public uint gMask = 0x0000FF00;
        public uint bMask = 0x000000FF;
        public uint aMask = 0x00000000;
        public List<Color32> palette;
        public Color32[] imageData;
        public Texture2D ToTexture2D()
        {
            var tex = new Texture2D(info.absWidth, info.absHeight);
            tex.SetPixels32(imageData);
            tex.Apply();
            return tex;
        }
    }


    public class BMPLoader
    {
        const ushort MAGIC = 0x4D42; // "BM" little endian
        public bool ReadPaletteAlpha = false;
        public bool ForceAlphaReadWhenPossible = false;

        public BMPImage LoadBMP(string aFileName)
        {
            using (var file = File.OpenRead(aFileName))
                return LoadBMP(file);
        }
        public BMPImage LoadBMP(byte[] aData)
        {
            using (var stream = new MemoryStream(aData))
                return LoadBMP(stream);
        }

        public BMPImage LoadBMP(Stream aData)
        {
            using (var reader = new BinaryReader(aData))
                return LoadBMP(reader);

        }
        public BMPImage LoadBMP(BinaryReader aReader)
        {
            BMPImage bmp = new BMPImage();
            if (!ReadFileHeader(aReader, ref bmp.header))
            {
                throw new FileLoadException("Not a valid BMP file.");
            }
            if (!ReadInfoHeader(aReader, ref bmp.info))
            {
                throw new FileLoadException("Unsupported header format");
            }
            if (   bmp.info.compressionMethod != BMPComressionMode.BI_RGB
                && bmp.info.compressionMethod != BMPComressionMode.BI_BITFIELDS
                && bmp.info.compressionMethod != BMPComressionMode.BI_ALPHABITFIELDS
                && bmp.info.compressionMethod != BMPComressionMode.BI_RLE4
                && bmp.info.compressionMethod != BMPComressionMode.BI_RLE8
                )
            {
                throw new FileLoadException($"Unsupported image format: {bmp.info.compressionMethod}");
            }
            long offset = 14 + bmp.info.size;
            aReader.BaseStream.Seek(offset, SeekOrigin.Begin);
            if (bmp.info.nBitsPerPixel < 24)
            {
                bmp.rMask = 0x00007C00;
                bmp.gMask = 0x000003E0;
                bmp.bMask = 0x0000001F;
            }

            if (bmp.info.compressionMethod == BMPComressionMode.BI_BITFIELDS || bmp.info.compressionMethod == BMPComressionMode.BI_ALPHABITFIELDS)
            {
                bmp.rMask = aReader.ReadUInt32();
                bmp.gMask = aReader.ReadUInt32();
                bmp.bMask = aReader.ReadUInt32();
            }
            if (ForceAlphaReadWhenPossible)
                bmp.aMask = GetMask(bmp.info.nBitsPerPixel) ^ (bmp.rMask | bmp.gMask | bmp.bMask);

            if (bmp.info.compressionMethod == BMPComressionMode.BI_ALPHABITFIELDS)
                bmp.aMask = aReader.ReadUInt32();

            if (bmp.info.nPaletteColors > 0 || bmp.info.nBitsPerPixel <= 8)
                bmp.palette = ReadPalette(aReader, bmp, ReadPaletteAlpha || ForceAlphaReadWhenPossible);


            aReader.BaseStream.Seek(bmp.header.offset, SeekOrigin.Begin);
            bool uncompressed = bmp.info.compressionMethod == BMPComressionMode.BI_RGB ||
                bmp.info.compressionMethod == BMPComressionMode.BI_BITFIELDS ||
                bmp.info.compressionMethod == BMPComressionMode.BI_ALPHABITFIELDS;
            switch (bmp.info.nBitsPerPixel)
            {
                case 32 when uncompressed:
                    Read32BitImage(aReader, bmp);
                    break;
                case 24 when uncompressed:
                    Read24BitImage(aReader, bmp);
                    break;
                case 16 when uncompressed:
                    Read16BitImage(aReader, bmp);
                    break;
                default:
                {
                    switch (bmp.info.compressionMethod)
                    {
                        case BMPComressionMode.BI_RLE4 when bmp.info.nBitsPerPixel == 4 && bmp.palette != null:
                            ReadIndexedImageRLE4(aReader, bmp);
                            break;
                        case BMPComressionMode.BI_RLE8 when bmp.info.nBitsPerPixel == 8 && bmp.palette != null:
                            ReadIndexedImageRLE8(aReader, bmp);
                            break;
                        case BMPComressionMode.BI_RGB:
                        case BMPComressionMode.BI_BITFIELDS:
                        case BMPComressionMode.BI_JPEG:
                        case BMPComressionMode.BI_PNG:
                        case BMPComressionMode.BI_ALPHABITFIELDS:
                        case BMPComressionMode.BI_CMYK:
                        case BMPComressionMode.BI_CMYKRLE8:
                        case BMPComressionMode.BI_CMYKRLE4:
                        default:
                        {
                            if (uncompressed && bmp.info.nBitsPerPixel <= 8 && bmp.palette != null)
                                ReadIndexedImage(aReader, bmp);
                            else
                            {
                                throw new FileLoadException($"Unsupported file format: {bmp.info.compressionMethod} BPP: {bmp.info.nBitsPerPixel}");
                            }

                            break;
                        }
                    }

                    break;
                }
            }
            return bmp;
        }


        private static void Read32BitImage(BinaryReader aReader, BMPImage bmp)
        {
            int w = Mathf.Abs(bmp.info.width);
            int h = Mathf.Abs(bmp.info.height);
            Color32[] data = bmp.imageData = new Color32[w * h];
            if (aReader.BaseStream.Position + w*h*4 > aReader.BaseStream.Length)
            {
                throw new FileLoadException("Unexpected end of file on BMP stream");
            }
            int shiftR = GetShiftCount(bmp.rMask);
            int shiftG = GetShiftCount(bmp.gMask);
            int shiftB = GetShiftCount(bmp.bMask);
            int shiftA = GetShiftCount(bmp.aMask);
            byte a = 255;
            for (int i = 0; i < data.Length; i++)
            {
                uint v = aReader.ReadUInt32();
                byte r = (byte)((v & bmp.rMask) >> shiftR);
                byte g = (byte)((v & bmp.gMask) >> shiftG);
                byte b = (byte)((v & bmp.bMask) >> shiftB);
                if (bmp.bMask != 0)
                    a = (byte)((v & bmp.aMask) >> shiftA);
                data[i] = new Color32(r,g,b,a);
            }
        }


        private static void Read24BitImage(BinaryReader aReader, BMPImage bmp)
        {
            var w = Mathf.Abs(bmp.info.width);
            var h = Mathf.Abs(bmp.info.height);
            var rowLength = ((24 * w + 31) / 32) * 4;
            var count = rowLength * h;
            var pad = rowLength - w * 3;
            var data = bmp.imageData = new Color32[w * h];
            if (aReader.BaseStream.Position + count > aReader.BaseStream.Length)
            {
                throw new FileLoadException($"Unexpected end of file. (Have {(aReader.BaseStream.Position + count)} bytes, expected {aReader.BaseStream.Length} bytes)");
            }
            var shiftR = GetShiftCount(bmp.rMask);
            var shiftG = GetShiftCount(bmp.gMask);
            var shiftB = GetShiftCount(bmp.bMask);
            for (var y = 0; y < h; y++)
            {
                for(var x = 0; x < w; x++)
                {
                    var v = aReader.ReadByte() | ((uint)aReader.ReadByte()<<8) | ((uint)aReader.ReadByte()<<16);
                    var r = (byte)((v & bmp.rMask) >> shiftR);
                    var g = (byte)((v & bmp.gMask) >> shiftG);
                    var b = (byte)((v & bmp.bMask) >> shiftB);
                    data[x+y*w] = new Color32(r, g, b, 255);
                }
                for (var i = 0; i < pad; i++)
                    aReader.ReadByte();
            }
        }

        private static void Read16BitImage(BinaryReader aReader, BMPImage bmp)
        {
            var w = Mathf.Abs(bmp.info.width);
            var h = Mathf.Abs(bmp.info.height);
            var rowLength = ((16 * w + 31) / 32) * 4;
            var count = rowLength * h;
            var pad = rowLength - w * 2;
            var data = bmp.imageData = new Color32[w * h];
            if (aReader.BaseStream.Position + count > aReader.BaseStream.Length)
            {
                throw new FileLoadException($"Unexpected end of file. (Have {aReader.BaseStream.Position + count} bytes, expected {aReader.BaseStream.Length} bytes)");
            }
            var shiftR = GetShiftCount(bmp.rMask);
            var shiftG = GetShiftCount(bmp.gMask);
            var shiftB = GetShiftCount(bmp.bMask);
            var shiftA = GetShiftCount(bmp.aMask);
            byte a = 255;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var v = aReader.ReadByte() | ((uint)aReader.ReadByte() << 8);
                    var r = (byte)((v & bmp.rMask) >> shiftR);
                    var g = (byte)((v & bmp.gMask) >> shiftG);
                    var b = (byte)((v & bmp.bMask) >> shiftB);
                    if (bmp.aMask != 0)
                        a = (byte)((v & bmp.aMask) >> shiftA);
                    data[x + y * w] = new Color32(r, g, b, a);
                }
                for (var i = 0; i < pad; i++)
                    aReader.ReadByte();
            }
        }

        private static void ReadIndexedImage(BinaryReader aReader, BMPImage bmp)
        {
            var w = Mathf.Abs(bmp.info.width);
            var h = Mathf.Abs(bmp.info.height);
            var bitCount = bmp.info.nBitsPerPixel;
            var rowLength = ((bitCount * w + 31) / 32) * 4;
            var count = rowLength * h;
            var pad = rowLength - (w * bitCount + 7)/8;
            var data = bmp.imageData = new Color32[w * h];
            if (aReader.BaseStream.Position + count > aReader.BaseStream.Length)
            {
                throw new FileLoadException($"Unexpected end of file. (Have {aReader.BaseStream.Position + count} bytes, expected {aReader.BaseStream.Length} bytes)");
            }
            var bitReader = new BitStreamReader(aReader);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var v = (int)bitReader.ReadBits(bitCount);
                    if (v >= bmp.palette.Count)
                    {
                        throw new FileLoadException("Indexed bitmap has indices greater than it's color palette");
                    }
                    data[x + y * w] = bmp.palette[v];
                }
                bitReader.Flush();
                for (var i = 0; i < pad; i++)
                    aReader.ReadByte();
            }
        }
        private static void ReadIndexedImageRLE4(BinaryReader aReader, BMPImage bmp)
        {
            var w = Mathf.Abs(bmp.info.width);
            var h = Mathf.Abs(bmp.info.height);
            var data = bmp.imageData = new Color32[w * h];
            var x = 0;
            var y = 0;
            var yOffset = 0;
            while (aReader.BaseStream.Position < aReader.BaseStream.Length-1)
            {
                var count = (int)aReader.ReadByte();
                var d = aReader.ReadByte();
                if (count > 0)
                {   
                    for (var i = (count/2); i > 0; i--)
                    {
                        data[x++ + yOffset] = bmp.palette[(d>>4)&0x0F];
                        data[x++ + yOffset] = bmp.palette[d & 0x0F];
                    }
                    if ((count & 0x01) > 0)
                    {
                        data[x++ + yOffset] = bmp.palette[(d >> 4) & 0x0F];
                    }
                }
                else
                {
                    if (d == 0)
                    {
                        x = 0;
                        y += 1;
                        yOffset = y * w;
                    }
                    else if (d == 1)
                    {
                        break;
                    }
                    else if (d == 2)
                    {
                        x += aReader.ReadByte();
                        y += aReader.ReadByte();
                        yOffset = y * w;
                    }
                    else
                    {
                        for(var i = (d / 2); i > 0; i--)
                        {
                            var d2 = aReader.ReadByte();
                            data[x++ + yOffset] = bmp.palette[(d2 >> 4) & 0x0F];
                            data[x++ + yOffset] = bmp.palette[d2 & 0x0F];
                        }
                        if ((d & 0x01) > 0)
                        {
                            data[x++ + yOffset] = bmp.palette[(aReader.ReadByte() >> 4) & 0x0F];
                        }
                        if ((((d - 1) / 2) & 1) == 0)
                        {
                            aReader.ReadByte(); // padding (word alignment)
                        }
                    }
                }
            }
        }
        private static void ReadIndexedImageRLE8(BinaryReader aReader, BMPImage bmp)
        {
            var w = Mathf.Abs(bmp.info.width);
            var h = Mathf.Abs(bmp.info.height);
            var data = bmp.imageData = new Color32[w * h];
            var x = 0;
            var y = 0;
            var yOffset = 0;
            while (aReader.BaseStream.Position < aReader.BaseStream.Length - 1)
            {
                var count = (int)aReader.ReadByte();
                var d = aReader.ReadByte();
                if (count > 0)
                {
                    for (var i = count; i > 0; i--)
                    {
                        data[x++ + yOffset] = bmp.palette[d ];
                    }
                }
                else
                {
                    if (d == 0)
                    {
                        x = 0;
                        y += 1;
                        yOffset = y * w;
                    }
                    else if (d == 1)
                    {
                        break;
                    }
                    else if (d == 2)
                    {
                        x += aReader.ReadByte();
                        y += aReader.ReadByte();
                        yOffset = y * w;
                    }
                    else
                    {
                        for (int i = d; i > 0; i--)
                        {
                            data[x++ + yOffset] = bmp.palette[aReader.ReadByte()];
                        }
                        if ((d & 0x01) > 0)
                        {
                            aReader.ReadByte(); // padding (word alignment)
                        }
                    }
                }
            }
        }
        private static int GetShiftCount(uint mask)
        {
            for(var i = 0; i < 32; i++)
            {
                if ((mask & 0x01) > 0)
                    return i;
                mask >>= 1;
            }
            return -1;
        }
        private static uint GetMask(int bitCount)
        {
            uint mask = 0;
            for(var i = 0; i < bitCount; i++)
            {
                mask <<= 1;
                mask |= 0x01;
            }
            return mask;
        }
        private static bool ReadFileHeader(BinaryReader aReader, ref BMPFileHeader aFileHeader)
        {
            aFileHeader.magic = aReader.ReadUInt16();
            if (aFileHeader.magic != MAGIC)
                return false;
            aFileHeader.filesize = aReader.ReadUInt32();
            aFileHeader.reserved = aReader.ReadUInt32();
            aFileHeader.offset = aReader.ReadUInt32();
            return true;
        }
        private static bool ReadInfoHeader(BinaryReader aReader, ref BitmapInfoHeader aHeader)
        {
            aHeader.size = aReader.ReadUInt32();
            if (aHeader.size < 40)
                return false;
            aHeader.width = aReader.ReadInt32();
            aHeader.height = aReader.ReadInt32();
            aHeader.nColorPlanes = aReader.ReadUInt16();
            aHeader.nBitsPerPixel = aReader.ReadUInt16();
            aHeader.compressionMethod = (BMPComressionMode)aReader.ReadInt32();
            aHeader.rawImageSize = aReader.ReadUInt32();
            aHeader.xPPM = aReader.ReadInt32();
            aHeader.yPPM = aReader.ReadInt32();
            aHeader.nPaletteColors = aReader.ReadUInt32();
            aHeader.nImportantColors = aReader.ReadUInt32();
            var pad = (int)aHeader.size - 40;
            if (pad > 0)
                aReader.ReadBytes(pad);
            return true;
        }

        private static List<Color32> ReadPalette(BinaryReader aReader, BMPImage aBmp, bool aReadAlpha)
        {
            var count = aBmp.info.nPaletteColors;
            if (count == 0u)
                count = 1u << aBmp.info.nBitsPerPixel;
            var palette = new List<Color32>((int)count);
            for(var i = 0; i < count; i++)
            {
                var b = aReader.ReadByte();
                var g = aReader.ReadByte();
                var r = aReader.ReadByte();
                var a = aReader.ReadByte();
                if (!aReadAlpha)
                    a = 255;
                palette.Add(new Color32(r,g,b,a));
            }
            return palette;
        }

    }
    public class BitStreamReader
    {
        private readonly BinaryReader _mReader;
        private byte _mData;
        private int _mBits;
        
        public BitStreamReader(BinaryReader aReader)
        {
            _mReader = aReader;
        }
        public BitStreamReader(Stream aStream) : this(new BinaryReader(aStream)) { }

        private byte ReadBit()
        {
            if (_mBits > 0) return (byte)((_mData >> --_mBits) & 1);
            _mData = _mReader.ReadByte();
            _mBits = 8;
            return (byte)((_mData >> --_mBits) & 1);
        }

        public ulong ReadBits(int aCount)
        {
            var val = 0UL;
            if (aCount is <= 0 or > 32)
                throw new System.ArgumentOutOfRangeException(nameof(aCount), "aCount must be between 1 and 32 inclusive");
            for (var i = aCount-1; i>=0; i--)
                val |= (ulong)ReadBit() << i;
            return val;
        }
        public void Flush()
        {
            _mData = 0;
            _mBits = 0;
        }
    }
}