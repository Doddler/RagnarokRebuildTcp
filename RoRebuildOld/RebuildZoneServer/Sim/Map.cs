using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Leopotam.Ecs;
using RebuildData.Server.Config;
using RebuildData.Server.Data.Types;
using RebuildData.Server.Logging;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Networking;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Sim
{
	public class Map
	{
		public int Id;
		public int Width;
		public int Height;
		public Chunk[] Chunks;
		public World World;

		public Area MapBounds;
		public Area ChunkBounds;
		public string Name;

		public MapWalkData WalkData;

		private int chunkWidth;
		private int chunkHeight;

		private int chunkCheckId;
		
		private int _playerCount;
		public int PlayerCount
		{
			get => _playerCount;
			set
			{
				if(value != _playerCount)
					ServerLogger.Debug($"Map {Name} changed player count to {value}.");
				_playerCount = value;
			}
		}

		private int entityCount = 0;

		private const int ChunkSize = 8;

		public void UpdatePlayerAfterMove(ref EcsEntity movingEntity, Character movingCharacter, Position oldPosition, Position newPosition)
		{
			Profiler.Event(ProfilerEvent.MapUpdatePlayerAfterMove);

			var movingPlayer = movingEntity.Get<Player>();

			var distance = oldPosition.SquareDistance(newPosition);
			var midPoint = (oldPosition + newPosition) / 2;
			var dist2 = ServerConfig.MaxViewDistance + (distance / 2) + 1;

			if (distance > ServerConfig.MaxViewDistance)
			{
				//ServerLogger.Log("Player moved long distance, having him remove all entities and reload.");
				//player can't see any of the old entities, so have them remove all of them, and add the new ones they see
				CommandBuilder.SendRemoveAllEntities(movingPlayer);
				SendAllEntitiesToPlayer(ref movingEntity);
				return;
			}
			
			//optimization idea: exclude chunks that are fully in both the old and new view, as they never need updating

			foreach (var chunk in GetChunkEnumeratorAroundPosition(midPoint, dist2))
			{
				foreach (var monster in chunk.Monsters)
				{
					var targetCharacter = monster.Get<Character>();

					//if the player couldn't see the entity before, and can now, have that player add the entity
					if (!targetCharacter.Position.InSquareRange(oldPosition, ServerConfig.MaxViewDistance) &&
						targetCharacter.Position.InSquareRange(newPosition, ServerConfig.MaxViewDistance))
						CommandBuilder.SendCreateEntity(targetCharacter, movingPlayer);

					//if the player could see the entity before, but can't now, have them remove the entity
					if (targetCharacter.Position.InSquareRange(oldPosition, ServerConfig.MaxViewDistance) &&
						!targetCharacter.Position.InSquareRange(newPosition, ServerConfig.MaxViewDistance))
						CommandBuilder.SendRemoveEntity(targetCharacter, movingPlayer, CharacterRemovalReason.OutOfSight);
				}

				foreach (var player in chunk.Players)
				{
					var targetCharacter = player.Get<Character>();

					//if the player couldn't see the entity before, and can now, have that player add the entity
					if (!targetCharacter.Position.InSquareRange(oldPosition, ServerConfig.MaxViewDistance) &&
						targetCharacter.Position.InSquareRange(newPosition, ServerConfig.MaxViewDistance))
						CommandBuilder.SendCreateEntity(targetCharacter, movingPlayer);

					//if the player could see the entity before, but can't now, have them remove the entity
					if (targetCharacter.Position.InSquareRange(oldPosition, ServerConfig.MaxViewDistance) &&
						!targetCharacter.Position.InSquareRange(newPosition, ServerConfig.MaxViewDistance))
						CommandBuilder.SendRemoveEntity(targetCharacter, movingPlayer, CharacterRemovalReason.OutOfSight);
				}
			}
		}

		public void TeleportEntity(ref EcsEntity entity, Character ch, Position newPosition, bool isWalkUpdate = false, CharacterRemovalReason reason = CharacterRemovalReason.Teleport)
		{
			var oldPosition = ch.Position;

			SendRemoveEntityAroundCharacter(ref entity, ch, reason);
			ch.Position = newPosition;
			SendAddEntityAroundCharacter(ref entity, ch);

			//check if the move puts them over to a new chunk, and if so, move them to the new one
			var cOld = GetChunkForPosition(oldPosition);
			var cNew = GetChunkForPosition(newPosition);

			if (cOld != cNew)
			{
				cOld.RemoveEntity(ref entity, ch.Type);
				cNew.AddEntity(ref entity, ch.Type);
			}

			//if the moving entity is a player, he needs to know of the new/removed entities from his sight
			if (ch.Type == CharacterType.Player)
			{
				var movingPlayer = entity.Get<Player>();
				CommandBuilder.SendRemoveAllEntities(movingPlayer);
				SendAllEntitiesToPlayer(ref entity);
			}
		}

		public void MoveEntity(ref EcsEntity entity, Character ch, Position newPosition, bool isWalkUpdate = false)
		{
			//if(ch.Type == CharacterType.Player)
			//	ServerLogger.Log($"Moving {entity} from {ch.Position} to {newPosition}");

			Profiler.Event(ProfilerEvent.MapMoveEntity);

			var oldPosition = ch.Position;
			var distance = ch.Position.SquareDistance(newPosition);

			//find the midpoint of the move and a distance that encloses both the starting and end points.
			var midPoint = (oldPosition + newPosition) / 2;
			var dist2 = ServerConfig.MaxViewDistance + (distance / 2) + 1;

			if (distance > ServerConfig.MaxViewDistance * 2 + 1)
			{
				//the character has moved more than one full screen, no entities that knew of the old position can see the new position
				SendRemoveEntityAroundCharacter(ref entity, ch, CharacterRemovalReason.OutOfSight);
				ch.Position = newPosition;
				SendAddEntityAroundCharacter(ref entity, ch);
			}
			else
			{
				//update all players in range of this entity
				foreach (var chunk in GetChunkEnumeratorAroundPosition(midPoint, dist2))
				{
					foreach (var player in chunk.Players)
					{
						var targetCharacter = player.Get<Character>();
						var playerObj = player.Get<Player>();

						if (targetCharacter == ch) //the player will get told separately of his own movement
							continue;

						if (!targetCharacter.IsActive)
							continue;

						if (!isWalkUpdate) //if you can see the start and end point, and it's just a walk update, the client can do the update themselves
						{
							if (targetCharacter.Position.InSquareRange(oldPosition, ServerConfig.MaxViewDistance) &&
								targetCharacter.Position.InSquareRange(newPosition, ServerConfig.MaxViewDistance))
							{
								CommandBuilder.AddRecipient(player);
								continue;
							}
						}

						//if the player couldn't see the entity before, and can now, have that player add the entity
						if (!targetCharacter.Position.InSquareRange(oldPosition, ServerConfig.MaxViewDistance) &&
							targetCharacter.Position.InSquareRange(newPosition, ServerConfig.MaxViewDistance))
							CommandBuilder.SendCreateEntity(ch, playerObj);

						//if the player could see the entity before, but can't now, have them remove the entity
						if (targetCharacter.Position.InSquareRange(oldPosition, ServerConfig.MaxViewDistance) &&
							!targetCharacter.Position.InSquareRange(newPosition, ServerConfig.MaxViewDistance))
							CommandBuilder.SendRemoveEntity(ch, playerObj, CharacterRemovalReason.OutOfSight);
					}
				}

				ch.Position = newPosition;
			}
			
			//if anyone has been batched as part of the move, send it to everyone
			if (CommandBuilder.HasRecipients())
			{
				CommandBuilder.SendMoveEntityMulti(ch);
				CommandBuilder.ClearRecipients();
			}

			//check if the move puts them over to a new chunk, and if so, move them to the new one
			var cOld = GetChunkForPosition(oldPosition);
			var cNew = GetChunkForPosition(newPosition);

			if (cOld != cNew)
			{
				cOld.RemoveEntity(ref entity, ch.Type);
				cNew.AddEntity(ref entity, ch.Type);
			}

			//if the moving entity is a player, he needs to know of the new/removed entities from his sight
			if (ch.Type == CharacterType.Player)
			{
				//ServerLogger.Log($"Sending update after move: {oldPosition} {newPosition}. Player is currently at: {ch.Position}");
				UpdatePlayerAfterMove(ref entity, ch, oldPosition, newPosition);
			}

		}

		public void AddEntity(ref EcsEntity entity)
		{
			var ch = entity.Get<Character>();
#if DEBUG
			if (ch == null)
				throw new Exception("Entity was added to map without Character object!");
#endif

			ch.Map = this;
			if (ch.IsActive)
				SendAddEntityAroundCharacter(ref entity, ch);

			var c = GetChunkForPosition(ch.Position);
			c.AddEntity(ref entity, ch.Type);

			if (ch.Type == CharacterType.Player)
				PlayerCount++;

			entityCount++;
		}

		public void SendAddEntityAroundCharacter(ref EcsEntity entity, Character ch)
		{
			foreach (Chunk chunk in GetChunkEnumeratorAroundPosition(ch.Position, ServerConfig.MaxViewDistance))
			{
				foreach (var player in chunk.Players)
				{
					var targetCharacter = player.Get<Character>();
					if (!targetCharacter.IsActive)
						continue;
					if (targetCharacter.Position.InSquareRange(ch.Position, ServerConfig.MaxViewDistance) && targetCharacter != ch)
						CommandBuilder.AddRecipient(player);
				}
			}

			if (CommandBuilder.HasRecipients())
			{
				CommandBuilder.SendCreateEntityMulti(ch);
				CommandBuilder.ClearRecipients();
			}
		}

		private void SendRemoveEntityAroundCharacter(ref EcsEntity entity, Character ch, CharacterRemovalReason reason)
		{
			foreach (Chunk chunk in GetChunkEnumeratorAroundPosition(ch.Position, ServerConfig.MaxViewDistance))
			{
				foreach (var player in chunk.Players)
				{
					var targetCharacter = player.Get<Character>();
					if (!targetCharacter.IsActive)
						continue;
					if (targetCharacter.Position.InSquareRange(ch.Position, ServerConfig.MaxViewDistance) && targetCharacter != ch)
						CommandBuilder.AddRecipient(player);
				}
			}

			if (CommandBuilder.HasRecipients())
			{
				CommandBuilder.SendRemoveEntityMulti(ch, reason);
				CommandBuilder.ClearRecipients();
			}
		}

		public void RemoveEntity(ref EcsEntity entity, CharacterRemovalReason reason)
		{
			if (!entity.IsAlive())
				return;

			var ch = entity.Get<Character>();

			//if(ch.IsActive)
			SendRemoveEntityAroundCharacter(ref entity, ch, reason);

			var hasRemoved = false;

			var charChunk = GetChunkForPosition(ch.Position);
			if (ch.Type == CharacterType.Player)
			{
				if (charChunk.Players.Remove(ref entity))
				{
					PlayerCount--;
					hasRemoved = true;
				}
			}
			else
			{
				if(charChunk.Monsters.Remove(ref entity))
					hasRemoved = true;
			}

			ch.Map = null;
			if(hasRemoved)
				entityCount--;
		}

		public void StartMove(ref EcsEntity entity, Character ch)
		{
			if (!entity.IsAlive())
				return;

			foreach (Chunk chunk in GetChunkEnumeratorAroundPosition(ch.Position, ServerConfig.MaxViewDistance))
			{
				foreach (var player in chunk.Players)
				{
					var targetCharacter = player.Get<Character>();
					if (!targetCharacter.IsActive)
						continue;
					if (targetCharacter.Position.InSquareRange(ch.Position, ServerConfig.MaxViewDistance))
						CommandBuilder.AddRecipient(player);
				}
			}

			if (CommandBuilder.HasRecipients())
			{
				CommandBuilder.SendStartMoveEntityMulti(ch);
				CommandBuilder.ClearRecipients();
			}
		}

		public void SendAllEntitiesToPlayer(ref EcsEntity target)
		{
			var playerChar = target.Get<Character>();
			var playerObj = target.Get<Player>();

			foreach (Chunk c in GetChunkEnumeratorAroundPosition(playerChar.Position, ServerConfig.MaxViewDistance))
			{
				//ServerLogger.Log("Sending entities around " + playerChar.Position);
				foreach (var m in c.Monsters)
				{
					var ch = m.Get<Character>();
					var distance = ch.Position.SquareDistance(playerChar.Position);
#if DEBUG
					if (distance > ServerConfig.MaxViewDistance + ChunkSize)
						throw new Exception("Unexpected chunk check distance!");
#endif
					if (ch.Position.InSquareRange(playerChar.Position, ServerConfig.MaxViewDistance))
						CommandBuilder.SendCreateEntity(ch, playerObj);

				}

				foreach (var p in c.Players)
				{
					var ch = p.Get<Character>();
					if (!ch.IsActive)
						continue;
					if (ch.Position.InSquareRange(playerChar.Position, ServerConfig.MaxViewDistance))
					{
						ServerLogger.Debug($"Sending create player entity for {playerObj.Entity}");
						CommandBuilder.SendCreateEntity(ch, playerObj);
					}
				}
			}
		}

		public void GatherPlayersForMultiCast(ref EcsEntity target, Character character)
		{
			foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, ServerConfig.MaxViewDistance))
			{
				foreach (var p in c.Players)
				{
					var ch = p.Get<Character>();
					if (!ch.Position.InSquareRange(character.Position, ServerConfig.MaxViewDistance))
						continue;
					if (ch.IsActive)
						CommandBuilder.AddRecipient(p);
				}
			}
		}
		
        public void GatherMonstersOfTypeInRange(Character character, int distance, EntityList list, MonsterDatabaseInfo monsterType)
        {
            Profiler.Event(ProfilerEvent.MapGatherEntities);

            foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, distance))
            {
                foreach (var m in c.Monsters)
                {
                    var ch = m.Get<Character>();
                    if (!ch.IsActive)
                        continue;

                    var monster = m.Get<Monster>();

                    if (monster.MonsterBase != monsterType)
                        return;
					
                    if (character.Position.InSquareRange(ch.Position, distance))
                        list.Add(m);
                }
            }
        }

		public void GatherEntitiesInRange(Character character, int distance, EntityList list, bool checkImmunity = false)
		{
			Profiler.Event(ProfilerEvent.MapGatherEntities);

			foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, ServerConfig.MaxViewDistance))
			{

				foreach (var m in c.Monsters)
				{
					var ch = m.Get<Character>();
					if (!ch.IsActive)
						continue;

					if (checkImmunity && ch.SpawnImmunity > 0)
						continue;

					if (character.Position.InSquareRange(ch.Position, distance))
						list.Add(m);
				}

				foreach (var p in c.Players)
				{
					var ch = p.Get<Character>();
					if (!ch.IsActive)
						continue;

					if (checkImmunity && ch.SpawnImmunity > 0)
						continue;

					if (character.Position.InSquareRange(ch.Position, distance))
						list.Add(p);
				}
			}
		}

        public bool QuickCheckPlayersNearby(Character character, int distance)
        {
            foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, distance))
            {
				if(c.Players.Count > 0)
                    return true;
            }

            return false;
        }

		public void GatherPlayersInRange(Character character, int distance, EntityList list, bool checkLineOfSight, bool checkImmunity = false)
		{
			Profiler.Event(ProfilerEvent.MapGatherPlayers);

			foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, ServerConfig.MaxViewDistance))
			{
				foreach (var p in c.Players)
				{
					var ch = p.Get<Character>();
					if (!ch.IsActive)
						continue;

                    if (checkImmunity)
                    {
                        if(ch.SpawnImmunity > 0 || ch.State == CharacterState.Dead)
                            continue;
                    }
					
                    if (character.Position.InSquareRange(ch.Position, distance))
                    {
                        if (checkLineOfSight && !WalkData.HasLineOfSight(character.Position, ch.Position))
                            continue;
                        list.Add(p);
                    }
                }
			}
		}

		private void ClearInactive(int i)
		{
			PlayerCount -= Chunks[i].Players.ClearInactive();
			Chunks[i].Monsters.ClearInactive();
		}

		public void Update()
		{
			chunkCheckId++;
			if (chunkCheckId >= chunkWidth * chunkHeight)
				chunkCheckId = 0;

			ClearInactive(chunkCheckId);
#if DEBUG
			if (PlayerCount < 0)
				throw new Exception("Player count has become an impossible value!");

			if (chunkCheckId == 0) //don't do it all the time
			{
				//sanitycheck
				var entityCount = 0;
				var loopCount = 0;
				var noEntities = 0;
				for (var i = 0; i < Chunks.Length; i++)
				{
					loopCount++;
					entityCount += Chunks[i].Players.Count;
					entityCount += Chunks[i].Monsters.Count;
					if (Chunks[i].Monsters.Count == 0)
						noEntities++;
				}

				var entityCount2 = 0;
				var loopCount2 = 0;
				var noEntities2 = 0;
				foreach (var chunk in GetChunkEnumerator(ChunkBounds))
				{
					loopCount2++;
					entityCount2 += chunk.Players.Count;
					entityCount2 += chunk.Monsters.Count;
					if (chunk.Monsters.Count == 0)
						noEntities2++;
				}

				if (entityCount != entityCount2)
					ServerLogger.LogError(
						$"FUUUUUUUCCCCKKKK! Entity count does not match expected value! Got {entityCount2}, expected {entityCount}");

				if (entityCount != this.entityCount)
					ServerLogger.LogError(
						$"Entity count does not match expected value! Has {entityCount}, expected {this.entityCount}");
			}
#endif
		}

		public ChunkAreaEnumerator GetChunkEnumeratorAroundPosition(Position p, int distance)
		{
			var area = Area.CreateAroundPoint(p, distance).ClipArea(MapBounds);
			var area2 = GetChunksForArea(area);
			return new ChunkAreaEnumerator(Chunks, chunkWidth, area2);
		}

		public ChunkAreaEnumerator GetChunkEnumerator(Area area)
		{
			return new ChunkAreaEnumerator(Chunks, chunkWidth, area);
		}

		private int AlignValue(int value, int alignment)
		{
			var remainder = value % alignment;
			if (remainder == 0)
				return value;
			return value - remainder + alignment;
		}

		public Chunk GetChunkForPosition(Position pos)
		{
			return Chunks[(pos.X / 8) + (pos.Y / 8) * chunkWidth];
		}

		public Area GetChunksForArea(Area area)
		{
			return new Area(area.MinX / 8, area.MinY / 8, area.MaxX / 8, area.MaxY / 8);
		}

		public bool FindPositionInRange(Area area, out Position p)
        {
            if (WalkData.FindWalkableCellInArea(area, out p))
                return true;

            ServerLogger.Debug($"Could not find walkable tile in {area} on map {Name} within 100 checks. Falling back to tile scan.");

			if (WalkData.ScanForWalkableCell(area, out p))
                return true;

			ServerLogger.LogWarning($"Attempted to find walkable cell in area {area} on map {Name}, but there are no walkable cells in the zone.");
			p = Position.Invalid;
            return false;
        }

		public Map(World world, string name, string walkData)
		{
			World = world;
			Name = name;

			WalkData = new MapWalkData(walkData);

			Width = WalkData.Width;
			Height = WalkData.Height;

			chunkWidth = AlignValue(Width, 8) / 8;
			chunkHeight = AlignValue(Height, 8) / 8;

			MapBounds = new Area(0, 0, Width - 1, Height - 1);
			ChunkBounds = new Area(0, 0, chunkWidth - 1, chunkHeight - 1);

			Chunks = new Chunk[chunkWidth * chunkHeight];

			PlayerCount = 0;

			var id = 0;
			var fullUnwalkable = 0;
			for (var x = 0; x < chunkWidth; x++)
			{
				for (var y = 0; y < chunkHeight; y++)
				{
					var walkable = 0;
					for (var x2 = 0; x2 < ChunkSize; x2++)
					{
						for (var y2 = 0; y2 < ChunkSize; y2++)
						{
							if (WalkData.IsCellWalkable(x * ChunkSize + x2, y * ChunkSize + y2))
								walkable++;
						}
					}
					Chunks[x + y * chunkWidth] = new Chunk();
					Chunks[x + y * chunkWidth].X = x;
					Chunks[x + y * chunkWidth].Y = y;
					Chunks[x + y * chunkWidth].WalkableTiles = walkable;

					if (walkable == 0)
						fullUnwalkable++;
					id++;
				}
			}

			//if(fullUnwalkable > 0)
			//	ServerLogger.Log($"Map has {fullUnwalkable} of {chunkWidth * chunkHeight} chunks without walkable cells.");
		}
	}
}
