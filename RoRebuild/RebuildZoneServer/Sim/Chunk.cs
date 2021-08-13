using System;
using System.Collections.Generic;
using System.Text;
using Leopotam.Ecs;
using RebuildData.Shared.Enum;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Sim
{
	public class Chunk
	{
		public int X;
		public int Y;
		public int WalkableTiles;
		public EntityList Players = new EntityList(16);
		public EntityList Monsters = new EntityList(16);

		public void AddEntity(ref EcsEntity entity, CharacterType type)
		{
#if DEBUG
			//sanity check
			var character = entity.Get<Character>();
			var chunkX = character.Position.X / 8;
			var chunkY = character.Position.Y / 8;
			if(X != chunkX || Y != chunkY)
				throw new Exception("Sanity check failed: Entity added to incorrect chunk?");
#endif
			switch (type)
			{
				case CharacterType.Player:
					Players.Add(ref entity);
					break;
				case CharacterType.Monster:
					Monsters.Add(ref entity);
					break;
				default:
					throw new Exception("Unhandled character type: " + type);
			}
		}

		public void RemoveEntity(ref EcsEntity entity, CharacterType type)
		{
			switch (type)
			{
				case CharacterType.Player:
					Players.Remove(ref entity);
					break;
				case CharacterType.Monster:
					Monsters.Remove(ref entity);
					break;
				default:
					throw new Exception("Unhandled character type: " + type);
			}
		}
	}
}
