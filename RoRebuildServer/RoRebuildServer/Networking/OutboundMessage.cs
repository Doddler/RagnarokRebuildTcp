using System.Buffers;
using System.Diagnostics;
using System.Text;
using K4os.Compression.LZ4;
using Lidgren.Network;
using Microsoft.Extensions.ObjectPool;
using RebuildSharedData.Networking;
using RebuildSharedData.Util;
using RoRebuildServer.Networking;

namespace RebuildZoneServer.Networking;

public class OutboundMessagePooledObjectPolicy : IPooledObjectPolicy<OutboundMessage>
{
    public OutboundMessage Create()
    {
        return new OutboundMessage();
    }

    public bool Return(OutboundMessage obj)
    {
        obj.Clear();
        return true;
    }
}

public class OutboundMessage : IBinaryMessageWriter
{
    public List<NetworkConnection> Clients;
    public byte[] Message;
    public bool IsInitialized;
    public bool IsQueued;
    public int Length => (position + 7) / 8; //convert position in bits to bytes

    private int position;
    

    public OutboundMessage()
    {
        Clients = new List<NetworkConnection>(10);
        Message = new byte[1024];
        position = 0;
        IsInitialized = false;
        IsQueued = false;
    }
    
    public OutboundMessage(List<NetworkConnection> clients, byte[] message, int length)
    {
        Clients = clients;
        Message = message;
        position = 0;
        IsInitialized = true;
    }

    public void Clear()
    {
        Clients.Clear();
        position = 0;
        IsInitialized = false;
        IsQueued = false;
    }

    public void WritePacketType(PacketType type)
    {
        Write((byte)type);
    }

    private void EnsureBufferSize(int length)
    {
        while (position + length > Message.Length * 8)
        {
            Debug.Assert(Message.Length * 2 < 10_000, "Attempting to resize the outbound message buffer to way too big!");
            Array.Resize(ref Message, Message.Length * 2);
        }
    }

    public void Write(byte b)
    {
        EnsureBufferSize(8);
        NetBitWriter.WriteByte(b, 8, Message, position);
        position += 8;
    }

    public void Write(int i)
    {
        EnsureBufferSize(32);
        NetBitWriter.WriteUInt32((uint)i, 32, Message, position);
        position += 32;
    }

    public void Write(uint i)
    {
        EnsureBufferSize(32);
        NetBitWriter.WriteUInt32((uint)i, 32, Message, position);
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

    public void Write(float f)
    {
        //taken from lidgren library source
        // Use union to avoid BitConverter.GetBytes() which allocates memory on the heap
        SingleUIntUnion su;
        su.UIntValue = 0; // must initialize every member of the union to avoid warning
        su.SingleValue = f;

        Write(su.UIntValue);
    }

    public void Write(bool b)
    {
        EnsureBufferSize(1);
        NetBitWriter.WriteByte((b ? (byte)1 : (byte)0), 1, Message, position);
        position += 1;
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

    public void Write(Memory<byte> b, int length)
    {
        EnsureBufferSize(length * 8);
        NetBitWriter.WriteBytes(b, 0, length, Message, position);
        position += length * 8;
    }

    public void WriteCompressedByteArray(byte[] b) => WriteCompressedByteArray(b, b.Length);

    public void WriteCompressedByteArray(byte[] b, int length)
    {
        Debug.Assert(length < ushort.MaxValue);
        var comp = ArrayPool<byte>.Shared.Rent(LZ4Codec.MaximumOutputSize(length));
        var newLen = LZ4Codec.Encode(b, comp);
        Write((ushort)newLen); //I assume we won't send more than 65535 bytes to the player at once but... who knows?
        Write(comp, newLen);
        ArrayPool<byte>.Shared.Return(comp);
    }

    public void Write(string s)
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
}