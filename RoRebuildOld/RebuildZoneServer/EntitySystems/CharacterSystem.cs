using System;
using System.Collections.Generic;
using System.Text;
using Leopotam.Ecs;
using RebuildData.Shared.Enum;
using RebuildZoneServer.EntityComponents;

namespace RebuildZoneServer.EntitySystems
{
	class CharacterSystem : IEcsRunSystem
	{
		private EcsWorld world = null;
		private EcsFilter<Character> characterFilter = null;
		public void Run()
		{
			foreach (var cId in characterFilter)
			{
				ref var c = ref characterFilter.Get1[cId];

				if (c.IsActive)
					c.Update();
			}
		}
	}
}
