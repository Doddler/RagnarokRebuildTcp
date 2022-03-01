using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Leopotam.Ecs;
using Microsoft.Extensions.ObjectPool;

namespace RebuildZoneServer.Util
{
	public static class EntityListPool
	{
		private static ObjectPool<EntityList> pool;

		private static bool isInitialized;

		public static void Initialize()
		{
			var defaultPolicy = new DefaultPooledObjectPolicy<EntityList>();
			pool = new DefaultObjectPool<EntityList>(defaultPolicy);
			isInitialized = true;
		}

		public static EntityList Get()
		{
			if(!isInitialized)
				Initialize();
			return pool.Get();
		}

		public static void Return(EntityList e)
		{
			if (!isInitialized)
				Initialize();
			e.Clear();
			pool.Return(e);
		}
	}
}
