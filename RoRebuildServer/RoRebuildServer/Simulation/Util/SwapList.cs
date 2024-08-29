using System.Diagnostics;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Simulation.Util;

public class SwapList<T> where T : struct, IEquatable<T>
{
	private T[] entities;
	//private Dictionary<EcsEntity, int> entityLookup;

	private int count;
	private int capacity;

	public int Count => count;

	public SwapList() : this(32) { }

	public SwapList(int initialCapacity)
	{
		capacity = initialCapacity;
		count = 0;
		entities = new T[capacity];
		//entityLookup = new Dictionary<EcsEntity, int>(capacity);
	}

	public T this[int index]
	{
		get
		{
			if (index < 0 || index >= count)
				throw new IndexOutOfRangeException();
			return entities[index];
		}
	}

	private void ResizeIfNeeded()
	{
		if (count + 1 > capacity)
		{
			capacity *= 2;
			Array.Resize(ref entities, capacity);
		}
	}

	public void Add(ref T entity)
	{
		ResizeIfNeeded();

		entities[count] = entity;
		count++;
	}


	public void Add(T entity)
	{
		ResizeIfNeeded();

		entities[count] = entity;
		count++;
	}

	public void Clear()
	{
		Array.Clear(entities, 0, capacity);
		count = 0;
	}

	public void SwapFromBack(int index)
	{
		//if(!entities[count - 1].IsAlive())
		//	throw new Exception("The last entity of EntityList is not active to perform SwapFromBack.");

		entities[index] = entities[count - 1];
		//entityLookup[entities[index]] = index;
		//entities[count - 1] = default;

		count--;
	}

    public void Remove(int index)
    {
		Debug.Assert(index < count);
		SwapFromBack(index);
    }

	public bool Remove(ref T entity)
	{
		//if(entityLookup.TryGetValue(entity, out var id))
		for (var i = 0; i < count; i++)
		{
			if (entities[i].Equals(entity))
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

	public bool Contains(T entity)
	{
		for (var i = 0; i < count; i++)
		{
			if (entities[i].Equals(entity))
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
		private readonly SwapList<T> entityList;
		private readonly int count;
		private int index;

		public T Current => entityList[index];

		internal Enumerator(SwapList<T> list)
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
}