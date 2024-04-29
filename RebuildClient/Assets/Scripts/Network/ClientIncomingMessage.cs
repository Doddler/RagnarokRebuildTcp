using System;
using System.Diagnostics;
using System.Text;
using Lidgren.Network;

namespace Assets.Scripts.Network
{
    class ClientInboundMessage
    {
        public byte[] Message;
        public int Length;

        private int position;

        private static byte[] buffer = new byte[1024];

        public ClientInboundMessage()
        {
            Message = new byte[1024];
            position = 0;
        }

        public ClientInboundMessage(byte[] message, int length)
        {
            Message = message;
            Length = length;
            position = 0;
        }

        public void Clear()
        {
            Length = 0;
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
        }

        [Conditional("DEBUG")]
        public void VerifyBufferSize(int size)
        {
            Debug.Assert(position + size <= Length * 8);
        }

        public byte ReadByte()
        {
            VerifyBufferSize(8);
            var ret = NetBitWriter.ReadByte(Message, 8, position);
            position += 8;
            return ret;
        }


        public void ReadBytes(byte[] buffer, int len)
        {
            VerifyBufferSize(len * 8);
            NetBitWriter.ReadBytes(Message, len, position, buffer, 0);
            position += len * 8;
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
            VerifyBufferSize(16);
            var ret = (ushort)NetBitWriter.ReadUInt16(Message, 16, position);
            position += 16;
            return ret;
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

            if ((position % 7) == 0)
            {
                var ret = BitConverter.ToSingle(Message, position / 8);
                position += 32;
                return ret;
            }

            ReadBytes(buffer, 4);
            var ret2 = BitConverter.ToSingle(buffer, 0);
            return ret2;
        }

        public string ReadString()
        {
            var len = ReadInt16();
            if (len == 0)
                return String.Empty;

            if (len > Length)
                return null; //don't allocate if they haven't sent enough bytes

            if ((position & 7) == 0)
            {
                var str2 = Encoding.UTF8.GetString(Message, position / 8, len);
                position += len * 8;
                return str2;
            }
            
            ReadBytes(buffer, len);
            var str = Encoding.UTF8.GetString(buffer, 0, len);
            
            return str;
        }
    }
}
