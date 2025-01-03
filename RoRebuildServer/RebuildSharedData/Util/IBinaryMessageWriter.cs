using RebuildSharedData.Networking;
using System;
using System.Collections.Generic;
using System.Text;
using K4os.Compression.LZ4;

namespace RebuildSharedData.Util;

public interface IBinaryMessageWriter
{
    int Length { get; }
    void Clear();
    void WritePacketType(PacketType type);
    void Write(byte b);
    void Write(int i);
    void Write(uint i);
    void Write(short s);
    void Write(ushort s);
    void Write(float f);
    void Write(bool b);
    void Write(byte[] b);
    void WriteCompressedByteArray(byte[] b);
    void WriteCompressedByteArray(byte[] b, int length);
    void Write(string s);
}

public interface IBinaryMessageReader
{
    int Length { get; }
    void Clear();
    byte ReadByte();
    byte[] ReadBytes(int len);
    void ReadBytes(byte[] buffer, int len);
    void ReadCompressedByteArray(byte[] buffer);
    void ReadCompressedByteArray(byte[] buffer, int len);
    int ReadInt32();
    short ReadInt16();
    ushort ReadUInt16();
    bool ReadBoolean();
    float ReadFloat();
    string ReadString();
    //Vector2Int ReadPosition();
}

public class BinaryMessageWriter(Stream output) : BinaryWriter(output), IBinaryMessageWriter
{
    public int Length => (int)BaseStream.Length;
    public void Clear()
    {
        throw new NotImplementedException();
    }

    public void WritePacketType(PacketType type)
    {
        Write((byte)type);
    }

    public void WriteCompressedByteArray(byte[] b)
    {
        LZ4Codec.MaximumOutputSize(1);
        throw new NotImplementedException();
        
    }

    public void WriteCompressedByteArray(byte[] b, int length)
    {
        throw new NotImplementedException();
    }
}

public class BinaryMessageReader(Stream input) : BinaryReader(input), IBinaryMessageReader
{
    public int Length { get; }
    public void Clear()
    {
        throw new NotImplementedException();
    }

    public void ReadBytes(byte[] buffer, int len)
    {
        var b = base.ReadBytes(len);
        Array.Copy(b, buffer, len);
    }

    public void ReadCompressedByteArray(byte[] buffer)
    {
        throw new NotImplementedException();
    }

    public void ReadCompressedByteArray(byte[] buffer, int len)
    {
        throw new NotImplementedException();
    }

    public float ReadFloat() => base.ReadSingle();
}