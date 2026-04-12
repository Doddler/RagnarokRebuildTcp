using System.Buffers;

namespace RoRebuildServer.Simulation.Util;

public class SerializationBuffer : IBufferWriter<byte>, IDisposable
{
    private byte[]? buffer;
    private int position;

    public int Position => position;

    public void Reset()
    {
        if(buffer != null)
            ArrayPool<byte>.Shared.Return(buffer);
    }

    public void Clear()
    {
        if (buffer == null)
            return;

        position = 0;
    }

    public void Advance(int count)
    {
        position += count;
    }

    public int CopyIntoBuffer(byte[] target, int offset)
    {
        if (buffer == null)
            return 0;
        if(target.Length + offset < position)
            Array.Resize(ref target, target.Length + offset);
        Array.Copy(buffer, 0, target, offset, position);
        return position;
    }
    
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (buffer == null)
            buffer = ArrayPool<byte>.Shared.Rent(sizeHint < 500 ? 500 : sizeHint);
        if (position + sizeHint > buffer.Length)
            Array.Resize(ref buffer, position + sizeHint + 100);
        return new Memory<byte>(buffer, position, sizeHint);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        if (buffer == null)
            buffer = ArrayPool<byte>.Shared.Rent(sizeHint < 500 ? 500 : sizeHint);
        if(position + sizeHint > buffer.Length)
            Array.Resize(ref buffer, position + sizeHint + 100);
        return new Span<byte>(buffer, position, sizeHint);
    }

    public void Dispose()
    {
        if(buffer != null)
            ArrayPool<byte>.Shared.Return(buffer);
    }
}
