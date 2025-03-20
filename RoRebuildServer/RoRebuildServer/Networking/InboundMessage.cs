using System.Buffers;
using System.Diagnostics;
using System.Text;
using Lidgren.Network;
using RebuildSharedData.Data;
using RebuildSharedData.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking;

public class InboundMessage : IBinaryMessageReader
{
    public NetworkConnection Client;
    public byte[] Message;
    public int Length { get; set; }

    private int position;

    public InboundMessage()
    {
        Client = null!;
        Message = new byte[1024];
        position = 0;
    }

    public InboundMessage(NetworkConnection client, byte[] message, int length)
    {
        Client = client;
        Message = message;
        Length = length;
        position = 0;
    }
    
    public void Clear()
    {
        Client = null!;
        Length = 0;
        position = 0;
    }

    public void Rewind()
    {
        position = 0;
    }

    private void EnsureBufferSize(int length)
    {
        while (position + length > Message.Length * 8)
        {
            Debug.Assert(Message.Length * 2 < 10_000, "Attempting to resize the outbound message buffer to way too big!");
            Array.Resize(ref Message, Message.Length * 2);
        }
    }

    public void Populate(byte[] src, int pos, int length)
    {
        EnsureBufferSize(length * 8);
        Buffer.BlockCopy(src, pos, Message, 0, length);
        Length = length;
    }

    [Conditional("DEBUG")]
    public void VerifyBufferSize(int size)
    {
        Debug.Assert(position + size <= Length * 8);
    }



    public sbyte ReadSByte()
    {
        VerifyBufferSize(8);
        var ret = NetBitWriter.ReadByte(Message, 8, position);
        position += 8;
        return (sbyte)ret;
    }
    
    public byte ReadByte()
    {
        VerifyBufferSize(8);
        var ret = NetBitWriter.ReadByte(Message, 8, position);
        position += 8;
        return ret;
    }

    public byte[] ReadBytes(int len)
    {
        throw new NotImplementedException();
    }


    public void ReadBytes(byte[] buffer, int len)
    {
        VerifyBufferSize(len * 8);
        NetBitWriter.ReadBytes(Message, len, position, buffer, 0);
        position += len * 8;
    }

    public void ReadCompressedByteArray(byte[] buffer)
    {
        throw new NotImplementedException();
    }

    public void ReadCompressedByteArray(byte[] buffer, int len)
    {
        throw new NotImplementedException();
    }


    public int ReadInt32()
    {
        VerifyBufferSize(32);
        var ret = (int)NetBitWriter.ReadUInt32(Message, 32, position);
        position += 32;
        return ret;
    }

    public short ReadInt16()
    {
        VerifyBufferSize(16);
        var ret = (short)NetBitWriter.ReadUInt16(Message, 16, position);
        position += 16;
        return ret;
    }

    public ushort ReadUInt16()
    {
        throw new NotImplementedException();
    }

    public bool ReadBoolean()
    {
        VerifyBufferSize(1);
        var ret = (short)NetBitWriter.ReadByte(Message, 1, position);
        position += 1;
        return (ret > 0 ? true : false);
    }

    public float ReadFloat()
    {
        VerifyBufferSize(32);
        SingleUIntUnion su;
        su.SingleValue = 0;
        su.UIntValue = NetBitWriter.ReadUInt32(Message, 32, position);
        position += 32;
        return su.SingleValue;
    }

    public Position ReadPosition()
    {
        VerifyBufferSize(32);
        var x = (int)NetBitWriter.ReadUInt16(Message, 16, position);
        position += 16;
        var y = (int)NetBitWriter.ReadUInt16(Message, 16, position);
        position += 16;
        return new Position(x, y);
    }

    public string ReadString()
    {
        var len = ReadInt16();
        if (len == 0)
            return String.Empty;

        if (len > Length)
        {
            throw new Exception($"Inbound packet from connection '{Client.Character?.Name}' sent packet which contained a malformed string.");
            //return null!; //don't allocate if they haven't sent enough bytes
        }

        if ((position & 7) == 0)
        {
            var str2 = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(Message, position / 8, len));
            position += len * 8;
            return str2;
        }

        var temp = ArrayPool<byte>.Shared.Rent(len);

        ReadBytes(temp, len);
        var str = Encoding.UTF8.GetString(temp, 0, len);

        ArrayPool<byte>.Shared.Return(temp);
        return str;
    }
}