using System;
using System.Collections.Generic;
using System.Text;
using Leopotam.Ecs;

namespace RebuildZoneServer.Util
{
	public class EntityList
	{
		private EcsEntity[] entities;
		//private Dictionary<EcsEntity, int> entityLookup;

		private int count;
		private int capacity;

		public int Count => count;

		public EntityList() : this(32) { }

		public EntityList(int initialCapacity)
		{
			capacity = initialCapacity;
			count = 0;
			entities = new EcsEntity[capacity];
			//entityLookup = new Dictionary<EcsEntity, int>(capacity);
		}

		public EcsEntity this[int index]
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

		public void Add(ref EcsEntity entity)
		{
			if(!entity.IsAlive())
				throw new Exception("Can't add entity to EntityList as it's not active.");

			ResizeIfNeeded();

			entities[count] = entity;
			count++;
		}


		public void Add(EcsEntity entity)
		{
			if (!entity.IsAlive())
				throw new Exception("Can't add entity to EntityList as it's not active.");

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

		public bool Remove(ref EcsEntity entity)
		{
			//if(entityLookup.TryGetValue(entity, out var id))
			for(var i = 0; i < count; i++)
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

		public bool Contains(EcsEntity entity)
		{
			for (var i = 0; i < count; i++)
			{
				if (entities[i] == entity)
					return true;
			}

			return false;
		}

		public int ClearInactive()
		{
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

			public EcsEntity Current => entityList[index];

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
	}
}
