using System.Runtime.CompilerServices;

namespace RoRebuildServer.EntitySystem;

public class GrowList<T>
{
    public T[] Items;
    public int Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GrowList(int capacity)
    {
        Items = new T[capacity];
        Count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if(Count >= Items.Length)
            Array.Resize(ref Items, Count << 1);

        Items[Count++] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop()
    {
        Count--;
        return Items[Count];
    }
}