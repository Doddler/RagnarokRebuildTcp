namespace RoRebuildServer.Simulation.Items;


public class GroundItemList : IDisposable
{
    private GroundItem[]? items;
    //private Dictionary<EcsEntity, int> entityLookup;

    private int count;
    private int capacity;

    public int Count => count;

    public GroundItem[]? InternalList => items;

    public GroundItemList() : this(8) { }

    public GroundItemList(int initialCapacity, bool delayCreate = false)
    {
        capacity = initialCapacity;
        count = 0;
        if (!delayCreate)
            items = new GroundItem[capacity];
    }

    public GroundItem this[int index]
    {
        get
        {
            if (index < 0 || index >= count || items == null)
                throw new IndexOutOfRangeException();
            return items[index];
        }
    }

    public void CopyEntities(GroundItemList other)
    {
        var otherList = other.InternalList;
        var otherCount = other.Count;
        if (otherList == null || otherCount == 0)
        {
            Clear();
            return;
        }
        items ??= new GroundItem[otherCount];
        if (items.Length < otherCount)
            Array.Resize(ref items, otherCount);
        Array.Copy(otherList, items, otherCount);
        count = otherCount;
    }

    private void ResizeIfNeeded()
    {
        items ??= new GroundItem[capacity];

        if (count + 1 > capacity)
        {
            capacity *= 2;
            Array.Resize(ref items, capacity);
        }
    }

    public void AddIfNotExists(ref GroundItem entity)
    {
        if (Contains(ref entity))
            return;

        ResizeIfNeeded();

        items![count] = entity;
        count++;
    }

    public void Add(ref GroundItem entity)
    {
        ResizeIfNeeded();

        items![count] = entity;
        count++;
    }

    public void Add(GroundItem entity)
    {
        ResizeIfNeeded();

        items![count] = entity;
        count++;
    }

    public void Clear()
    {
        if (items != null && count > 0)
            Array.Clear(items, 0, capacity);
        count = 0;
    }

    public void SwapFromBack(int index)
    {
        items![index] = items[count - 1];

        count--;
    }

    public bool Remove(ref GroundItem entity)
    {
        if (items == null)
            return false;

        for (var i = 0; i < count; i++)
        {

            if (items[i] == entity)
            {
                if (i == count - 1)
                {
                    items[i] = default;
                    count--;
                    return true;
                }

                SwapFromBack(i);

                return true;
            }
        }

        return false;
    }

    public bool Remove(int id)
    {
        if (items == null) return false;

        for (var i = 0; i < count; i++)
        {
            if (items[i].Id == id)
            {
                if (i == count - 1)
                {
                    items[i] = default;
                    count--;
                    return true;
                }

                SwapFromBack(i); 
                return true;
            }
        }

        return false;
    }

    public bool Contains(GroundItem entity)
    {
        if (items == null)
            return false;

        for (var i = 0; i < count; i++)
        {
            if (items[i] == entity)
                return true;
        }

        return false;
    }

    public bool Contains(ref GroundItem entity)
    {
        if (items == null)
            return false;

        for (var i = 0; i < count; i++)
        {
            if (items[i] == entity)
                return true;
        }

        return false;
    }

    public bool TryGet(int id, out GroundItem entity)
    {
        entity = default;
        if(items == null)
            return false;
        for(var i = 0;i < count;i++)
        {
            if (items[i].Id == id)
            {
                entity = items[i];
                return true;
            }
        }

        return false;
    }

    public bool Contains(int id)
    {
        if (items == null)
            return false;

        for(var i = 0; i < count; i++)
        {
            if (items[i].Id == id)
                return true;
        }

        return false;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    public struct Enumerator
    {
        private readonly GroundItemList entityList;
        private readonly int count;
        private int index;

        public GroundItem Current => entityList[index];

        internal Enumerator(GroundItemList list)
        {
            entityList = list;
            count = list.count;
            index = -1;
        }

        public bool MoveNext()
        {
            index++;
            return index < count;
        }
    }

    public void Dispose()
    {
        GroundItemListPool.Return(this);
    }
}