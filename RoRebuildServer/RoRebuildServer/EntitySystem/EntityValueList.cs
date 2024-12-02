using System.Numerics;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntitySystem
{
    public class EntityValueList<T> : IDisposable where T : INumber<T>
    {
        private Entity[]? entities;
        private T[]? values;
        //private Dictionary<EcsEntity, int> entityLookup;

        private int count;
        private int capacity;

        public int Count => count;

        private Entity[]? InternalList => entities;
        private T[]? InternalValueList => values;

        public EntityValueList() : this(32) { }

        public EntityValueList(int initialCapacity, bool delayCreate = false)
        {
            capacity = initialCapacity;
            count = 0;
            if (!delayCreate)
            {
                entities = new Entity[capacity];
                values = new T[capacity];
            }
        }

        public (Entity, T) this[int index]
        {
            get
            {
                if (index < 0 || index >= count || entities == null || values == null)
                    throw new IndexOutOfRangeException();
                return (entities[index], values[index]);
            }
        }

        public void CopyEntities(EntityValueList<T> other)
        {
            var otherList = other.InternalList;
            var otherValueList = other.InternalValueList;
            var otherCount = other.Count;
            if (otherList == null || otherCount == 0)
            {
                Clear();
                return;
            }
            entities ??= new Entity[otherCount];
            values ??= new T[otherCount];
            if (entities.Length < otherCount)
            {
                Array.Resize(ref entities, otherCount);
                Array.Resize(ref values, otherCount);
            }

            Array.Copy(otherList, entities, otherCount);
            Array.Copy(otherValueList, values, otherCount);
            count = otherCount;

        }

        private void ResizeIfNeeded()
        {
            entities ??= new Entity[capacity];
            values ??= new T[capacity];

            if (count + 1 > capacity)
            {
                capacity *= 2;
                Array.Resize(ref entities, capacity);
                Array.Resize(ref values, capacity);
            }
        }

        public void AddOrIncreaseValue(ref Entity entity, T value)
        {
            if (!entity.IsAlive())
                throw new Exception("Can't add entity to EntityValueList as it's not active.");
            
            ResizeIfNeeded();

            for (var i = 0; i < count; i++)
            {
                if (entities![i] == entity)
                {
                    values![i] += value;
                    return;
                }
            }

            entities![count] = entity;
            values![count] = value;
            count++;
        }

        public void AddOrSetValue(ref Entity entity, T value)
        {
            if (!entity.IsAlive())
                throw new Exception("Can't add entity to EntityValueList as it's not active.");

            ResizeIfNeeded();

            for (var i = 0; i < count; i++)
            {
                if (entities![i] == entity)
                {
                    values![i] = value;
                    return;
                }
            }

            entities![count] = entity;
            values![count] = value;
            count++;
        }


        public void Add(ref Entity entity, T value)
        {
            if (!entity.IsAlive())
                throw new Exception("Can't add entity to EntityValueList as it's not active.");

#if DEBUG
            if (Contains(ref entity))
                throw new Exception($"Attempting to double add an entity to an Entity Value Pool!");
#endif

            ResizeIfNeeded();

            entities![count] = entity;
            values![count] = value;
            count++;
        }


        public void Add(Entity entity, T value)
        {
            if (!entity.IsAlive())
                throw new Exception("Can't add entity to EntityValueList as it's not active.");

#if DEBUG
            if (Contains(ref entity))
                throw new Exception($"Attempting to double add an entity to an Entity Value Pool!");
#endif

            ResizeIfNeeded();

            entities![count] = entity;
            values![count] = value;
            count++;
        }

        public int CountEntitiesAboveValueAndRemoveBelow(T value)
        {
            if (count == 0 || values == null || entities == null)
                return 0;

            var match = 0;

            for (var i = 0; i < count; i++)
            {
                if (entities[i].IsAlive() && values[i] >= value)
                {
                    match++;
                    continue;
                }
                
                SwapFromBack(i);
                i--;
            }

            return match;
        }

        public void Clear()
        {
            if (entities != null && count > 0)
            {
                Array.Clear(entities, 0, capacity);
                Array.Clear(values, 0, capacity);
            }

            count = 0;
        }

        public void SwapFromBack(int index)
        {
            entities![index] = entities[count - 1];
            values![index] = values[count - 1];

            count--;
        }
        
        public bool Remove(ref Entity entity)
        {
            if (entities == null || values == null)
                return false;

            for (var i = 0; i < count; i++)
            {

                if (entities[i] == entity)
                {
                    if (i == count - 1)
                    {
                        entities[i] = default;
                        values[i] = default;
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
            private readonly EntityValueList<T> entityList;
            private readonly int count;
            private int index;

            public (Entity, T) Current => entityList[index];

            internal Enumerator(EntityValueList<T> list)
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
            EntityValueListPool<T>.Return(this);
        }
    }
}
