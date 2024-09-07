using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntitySystem;

public class EntityList : IDisposable
{
    private Entity[]? entities;
    //private Dictionary<EcsEntity, int> entityLookup;

    private int count;
    private int capacity;

    public int Count => count;

    public Entity[]? InternalList => entities;

    public EntityList() : this(32) { }

    public EntityList(int initialCapacity, bool delayCreate = false)
    {
        capacity = initialCapacity;
        count = 0;
        if (!delayCreate)
            entities = new Entity[capacity];
    }

    public Entity this[int index]
    {
        get
        {
            if (index < 0 || index >= count || entities == null)
                throw new IndexOutOfRangeException();
            return entities[index];
        }
    }

    public void CopyEntities(EntityList other)
    {
        var otherList = other.InternalList;
        var otherCount = other.Count;
        if (otherList == null || otherCount == 0)
        {
            Clear();
            return;
        }
        entities ??= new Entity[otherCount];
        if(entities.Length < otherCount)
            Array.Resize(ref entities, otherCount);
        Array.Copy(otherList, entities, otherCount);
        count = otherCount;

    }

    private void ResizeIfNeeded()
    {
        entities ??= new Entity[capacity];

        if (count + 1 > capacity)
        {
            capacity *= 2;
            Array.Resize(ref entities, capacity);
        }
    }

    public void AddIfNotExists(ref Entity entity)
    {
        if (!entity.IsAlive())
            throw new Exception("Can't add entity to EntityList as it's not active.");

        if (Contains(ref entity))
            return;

        ResizeIfNeeded();

        entities![count] = entity;
        count++;
    }


    public void Add(ref Entity entity)
    {
        if (!entity.IsAlive())
            throw new Exception("Can't add entity to EntityList as it's not active.");

        ResizeIfNeeded();

        entities![count] = entity;
        count++;
    }


    public void Add(Entity entity)
    {
        if (!entity.IsAlive())
            throw new Exception("Can't add entity to EntityList as it's not active.");

        ResizeIfNeeded();

        entities![count] = entity;
        count++;
    }

    public void Clear()
    {
        if (entities != null && count > 0)
            Array.Clear(entities, 0, capacity);
        count = 0;
    }

    public void SwapFromBack(int index)
    {
        entities![index] = entities[count - 1];
     
        count--;
    }

    public bool Remove(ref Entity entity)
    {
        if (entities == null) 
            return false;
        
        for (var i = 0; i < count; i++)
        {

            if (entities[i] == entity)
            {
                if (i == count - 1)
                {
                    entities[i] = default;
                    count--;
                    return true;
                }

                SwapFromBack(i);

                return true;
            }
        }

        return false;
    }

    public bool Contains(Entity entity)
    {
        if (entities == null)
            return false;

        for (var i = 0; i < count; i++)
        {
            if (entities[i] == entity)
                return true;
        }

        return false;
    }

    public bool Contains(ref Entity entity)
    {
        if (entities == null)
            return false;

        for (var i = 0; i < count; i++)
        {
            if (entities[i] == entity)
                return true;
        }

        return false;
    }

    public int ClearInactive()
    {
        if (entities == null)
            return 0;

        var clearCount = 0;
        for (var i = 0; i < count; i++)
        {
            if (!entities[i].IsAlive())
            {
                clearCount++;

                if (i == count - 1)
                {
                    //entities[i] = default;
                    count--;
                }
                else
                {
                    SwapFromBack(i);
                    i--;
                }
            }
        }

        return clearCount;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    public struct Enumerator
    {
        private readonly EntityList entityList;
        private readonly int count;
        private int index;

        public Entity Current => entityList[index];

        internal Enumerator(EntityList list)
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
        EntityListPool.Return(this);
    }
}