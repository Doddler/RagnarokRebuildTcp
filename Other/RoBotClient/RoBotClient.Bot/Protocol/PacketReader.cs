using System.Text;
using Lidgren.Network;
using MemoryPack;
using RebuildSharedData.Data;
using RebuildSharedData.Networking;

namespace RoBotClient.Bot.Protocol;

/// <summary>
/// Bit-packed inbound packet reader. Mirrors the server's InboundMessage / client's
/// ClientInboundMessage. Reads must occur in the exact field order the sender wrote them.
/// Wraps a single received WebSocket binary frame (one packet).
/// </summary>
public sealed class PacketReader
{
    public byte[] Message;
    public int Length { get; private set; } // bytes
    private int position; // in bits

    public int Position => position;
    public bool HasUnreadData => position < Length * 8;

    public PacketReader(byte[] message, int length)
    {
        Message = message;
        Length = length;
        position = 0;
    }

    public void Rewind() => position = 0;

    public PacketType ReadPacketType() => (PacketType)ReadByte();

    public byte ReadByte()
    {
        var r = NetBitWriter.ReadByte(Message, 8, position);
        position += 8;
        return r;
    }

    public sbyte ReadSByte() => (sbyte)ReadByte();

    public int ReadInt32()
    {
        var r = (int)NetBitWriter.ReadUInt32(Message, 32, position);
        position += 32;
        return r;
    }

    public uint ReadUInt32()
    {
        var r = NetBitWriter.ReadUInt32(Message, 32, position);
        position += 32;
        return r;
    }

    public short ReadInt16()
    {
        var r = (short)NetBitWriter.ReadUInt16(Message, 16, position);
        position += 16;
        return r;
    }

    public ushort ReadUInt16()
    {
        var r = NetBitWriter.ReadUInt16(Message, 16, position);
        position += 16;
        return r;
    }

    public bool ReadBoolean()
    {
        var r = NetBitWriter.ReadByte(Message, 1, position);
        position += 1;
        return r > 0;
    }

    public float ReadFloat()
    {
        SingleUIntUnion su;
        su.SingleValue = 0;
        su.UIntValue = NetBitWriter.ReadUInt32(Message, 32, position);
        position += 32;
        return su.SingleValue;
    }

    public void ReadBytes(byte[] buffer, int len)
    {
        NetBitWriter.ReadBytes(Message, len, position, buffer, 0);
        position += len * 8;
    }

    public byte[] ReadBytes(int len)
    {
        var buf = new byte[len];
        ReadBytes(buf, len);
        return buf;
    }

    public Position ReadPosition()
    {
        var x = (int)NetBitWriter.ReadUInt16(Message, 16, position);
        position += 16;
        var y = (int)NetBitWriter.ReadUInt16(Message, 16, position);
        position += 16;
        return new Position(x, y);
    }

    public string ReadString()
    {
        int len = ReadUInt16();
        if (len == 0)
            return string.Empty;

        var temp = new byte[len];
        ReadBytes(temp, len);
        return Encoding.UTF8.GetString(temp);
    }

    /// <summary>
    /// Reads an int32-length-prefixed MemoryPack blob, matching the server's
    /// OutboundMessage.MemoryPackSerializeWithLength (used by CreateEntity2 spawn data).
    /// </summary>
    public T? MemoryPackDeserializeWithLength<T>()
    {
        var len = ReadInt32();
        var bytePos = (position + 7) / 8; // writer byte-aligns the blob start
        var span = new ReadOnlySpan<byte>(Message, bytePos, len);
        var obj = MemoryPackSerializer.Deserialize<T>(span);
        position = (len + bytePos) * 8;
        return obj;
    }
}
