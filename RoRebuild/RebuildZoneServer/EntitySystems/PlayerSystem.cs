using System;
using System.Collections.Generic;
using System.Text;
using Leopotam.Ecs;
using RebuildData.Shared.Data;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Sim;

namespace RebuildZoneServer.EntitySystems
{
	class PlayerSystem : IEcsRunSystem
	{
		private World gameWorld = null;
		private EcsWorld world = null;
		private EcsFilter<Player> playerFilter = null;

		public void Run()
		{
			foreach (var pId in playerFilter)
			{
				ref var p = ref playerFilter.Get1[pId];

				if (!p.Character.IsActive)
					continue;

				p.Update();
				p.CombatEntity.Update();
			}
		}
	}
}
