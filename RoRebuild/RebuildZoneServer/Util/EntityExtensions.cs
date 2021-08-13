using System;
using System.Collections.Generic;
using System.Text;
using Leopotam.Ecs;

namespace RebuildZoneServer.Util
{
	public interface IStandardEntity
	{
		void Reset();
	}
	
	public static class EntityExtensions
	{
		public static T SetAndReset<T>(this EcsEntity entity) where T : class, IStandardEntity
		{
			var c = entity.Set<T>();
			c.Reset();
			return c;
		}

		public static EcsEntity AddAndReset<T1, T2, T3>(this EcsWorld world, out T1 c1, out T2 c2, out T3 c3) where T1 : class, IStandardEntity where T2 : class,IStandardEntity where T3 : class, IStandardEntity
		{
			var e = world.NewEntity();
			c1 = e.Set<T1>();
			c2 = e.Set<T2>();
			c3 = e.Set<T3>();

			c1.Reset();
			c2.Reset();
			c3.Reset();

			return e;
		}
	}
}
