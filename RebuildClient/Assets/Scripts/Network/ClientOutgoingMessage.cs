using System;
using System.Text;
using Lidgren.Network;
using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Assets.Scripts.Network
{
    class ClientOutgoingMessage
    {

        public byte[] Message;
        public int Length => (position + 7) / 8;

        private int position;

        public ClientOutgoingMessage()
        {
            Message = new byte[1024];
            position = 0;
        }

        public ClientOutgoingMessage(byte[] message, int length)
        {
            Message = message;
            position = 0;
        }

        public void Clear()
        {
            position = 0;
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
            EnsureBufferSize(32);
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

        public void Write(Vector2Int p)
        {
            Write((short)p.x);
            Write((short)p.y);
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
}
