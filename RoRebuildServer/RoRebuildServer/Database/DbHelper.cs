using RoRebuildServer.Logging;
using System.Buffers;
using RoRebuildServer.EntityComponents.Items;

namespace RoRebuildServer.Database
{
    public static class DbHelper
    {
        public static byte[] BorrowArrayAndWriteDictionary<T>(Dictionary<T, int>? dict) where T : Enum
        {
            var size = 4;
            
            if (dict != null)
                size = 8 * dict.Count + 1;
            
            var buffer = ArrayPool<byte>.Shared.Rent(size);

            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms);

            bw.Write(dict != null);
            if (dict == null)
                return buffer;

            bw.Write(dict.Count);
            foreach (var entry in dict)
            {
                bw.Write(Convert.ToInt32(entry.Key));
                bw.Write(entry.Value);
            }

            return buffer;
        }


        public static byte[] BorrowArrayAndWriteDictionary(Dictionary<string, int>? dict)
        {
            var size = 16;
            if (dict != null)
                size = 24 * dict.Count;
            var buffer = ArrayPool<byte>.Shared.Rent(size);

            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms);

            bw.Write(dict != null);
            if (dict == null)
                return buffer;

            bw.Write(dict.Count);

            foreach (var entry in dict)
            {
#if DEBUG
                if (entry.Key.Length > 16)
                    ServerLogger.LogWarning($"We're trying to write a string value longer than 16 bytes long! That could be bad.");
#endif
                bw.Write(entry.Key);
                bw.Write(entry.Value);
            }

            return buffer;
        }

        public static Dictionary<T, int>? ReadDictionary<T>(byte[]? buffer) where T : Enum
        {
            if (buffer == null || buffer.Length == 0)
                return null;

            using var ms = new MemoryStream(buffer);
            using var br = new BinaryReader(ms);

            if (!br.ReadBoolean())
                return null; //they have no dictionary to read

            var dict = new Dictionary<T, int>();

            var count = br.ReadInt32();
            var type = Enum.GetUnderlyingType(typeof(T));

            for (var i = 0; i < count; i++)
                dict.Add((T)Enum.ToObject(typeof(T), br.ReadInt32()), br.ReadInt32());

            return dict;
        }

        public static Dictionary<string, int>? ReadDictionary(byte[]? buffer)
        {
            if (buffer == null)
                return null;

            using var ms = new MemoryStream(buffer);
            using var br = new BinaryReader(ms);

            if (!br.ReadBoolean())
                return null; //they have no dictionary to read

            var dict = new Dictionary<string, int>();

            var count = br.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var key = br.ReadString();
                var value = br.ReadInt32();

                dict.Add(key, value);
            }

            return dict;
        }
    }
}
