using System.Text;
using Lidgren.Network;
using RebuildSharedData.Data;
using RebuildSharedData.Networking;

namespace RoBotClient.Bot.Protocol;

/// <summary>
/// Bit-packed outbound packet builder. Mirrors the server's OutboundMessage / client's
/// ClientOutgoingMessage wire format exactly (Lidgren NetBitWriter encoding): bool = 1 bit,
/// integers little-endian, float = 32-bit IEEE-754, string = ushort UTF-8-byte-length + bytes,
/// Position = two int16 (x, y). Each finished packet is sent as one WebSocket binary frame.
/// </summary>
public sealed class PacketWriter
{
    public byte[] Message;
    private int position; // in bits

    public int Position => position;
    public int Length => (position + 7) / 8; // position in bits -> bytes

    public PacketWriter(int initialCapacityBytes = 256)
    {
        Message = new byte[initialCapacityBytes];
        position = 0;
    }

    public void Clear()
    {
        Array.Clear(Message, 0, Message.Length);
        position = 0;
    }

    private void EnsureBufferSize(int bits)
    {
        while (position + bits > Message.Length * 8)
            Array.Resize(ref Message, Message.Length * 2);
    }

    public void WritePacketType(PacketType type) => Write((byte)type);

    public void Write(byte b)
    {
        EnsureBufferSize(8);
        NetBitWriter.WriteByte(b, 8, Message, position);
        position += 8;
    }

    public void Write(sbyte b) => Write((byte)b);

    public void Write(int i)
    {
        EnsureBufferSize(32);
        NetBitWriter.WriteUInt32((uint)i, 32, Message, position);
        position += 32;
    }

    public void Write(uint i)
    {
        EnsureBufferSize(32);
        NetBitWriter.WriteUInt32(i, 32, Message, position);
        position += 32;
    }

    public void Write(short s)
    {
        EnsureBufferSize(16);
        NetBitWriter.WriteUInt16((ushort)s, 16, Message, position);
        position += 16;
    }

    public void Write(ushort s)
    {
        EnsureBufferSize(16);
        NetBitWriter.WriteUInt16(s, 16, Message, position);
        position += 16;
    }

    public void Write(bool b)
    {
        EnsureBufferSize(1);
        NetBitWriter.WriteByte(b ? (byte)1 : (byte)0, 1, Message, position);
        position += 1;
    }

    public void Write(float f)
    {
        // Reinterpret the float bits as a uint without allocating (matches Lidgren / server).
        SingleUIntUnion su;
        su.UIntValue = 0;
        su.SingleValue = f;
        Write(su.UIntValue);
    }

    public void Write(byte[] b)
    {
        EnsureBufferSize(b.Length * 8);
        NetBitWriter.WriteBytes(b, 0, b.Length, Message, position);
        position += b.Length * 8;
    }

    public void Write(byte[] b, int length)
    {
        EnsureBufferSize(length * 8);
        NetBitWriter.WriteBytes(b, 0, length, Message, position);
        position += length * 8;
    }

    public void Write(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            Write((ushort)0);
            return;
        }

        var b = Encoding.UTF8.GetBytes(s);
        EnsureBufferSize(b.Length * 8 + 16);
        Write((ushort)b.Length);
        Write(b);
    }

    public void Write(Position p)
    {
        Write((short)p.X);
        Write((short)p.Y);
    }

    /// <summary>The finished packet bytes, ready to hand to ClientWebSocket.SendAsync (Binary).</summary>
    public ReadOnlyMemory<byte> AsMemory() => new(Message, 0, Length);

    public byte[] ToArray()
    {
        var a = new byte[Length];
        Buffer.BlockCopy(Message, 0, a, 0, Length);
        return a;
    }
}
