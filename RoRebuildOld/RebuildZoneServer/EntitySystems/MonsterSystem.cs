using System;
using System.Collections.Generic;
using System.Text;
using Leopotam.Ecs;
using RebuildData.Shared.Data;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.EntitySystems
{
	class MonsterSystem : IEcsRunSystem
	{
		private EcsWorld world = null;
		private EcsFilter<Monster> monsterFilter = null;
		public void Run()
		{
			foreach (var mId in monsterFilter)
			{
				ref var m = ref monsterFilter.Get1[mId];

				m.Update();
				if (m.Character.IsActive)
					m.CombatEntity.Update();

				//m.UpdateTime -= Time.DeltaTimeFloat;
				//if (m.UpdateTime < 0f)
				//{
				//	m.UpdateTime += 5f;

				//	var newPos = new Position() { X = GameRandom.Next(0, 255), Y = GameRandom.Next(0, 255) };

				//	ch.Map.MoveEntity(ref e, ref ch, newPos);
				//}
			}
		}
	}
}
