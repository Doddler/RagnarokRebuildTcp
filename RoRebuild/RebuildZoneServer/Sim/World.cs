using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Leopotam.Ecs;
using RebuildData.Server.Data.Types;
using RebuildData.Server.Logging;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using RebuildZoneServer.Data;
using RebuildZoneServer.Data.Management;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.EntitySystems;
using RebuildZoneServer.Networking;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.Sim
{
	public class World
	{
		public static World Instance;

		private readonly List<Map> Maps = new List<Map>();

		private Dictionary<int, EcsEntity> entityList = new Dictionary<int, EcsEntity>();
		private Dictionary<string, int> mapIdLookup = new Dictionary<string, int>();

		private List<EcsEntity> removeList = new List<EcsEntity>(30);

		private EcsWorld ecsWorld;
		private EcsSystems ecsSystems;

		private int nextEntityId = 0;
		private int maxEntityId = 10_000_000;

		private int mapCount = 0;
		const int initialEntityCount = 10_000;

		public World()
		{
			Instance = this;

			var initialMaxEntities = NextPowerOf2(initialEntityCount);
			if (initialMaxEntities < 1024)
				initialMaxEntities = 1024;

			ecsWorld = new EcsWorld(initialMaxEntities);

			ecsSystems = new EcsSystems(ecsWorld)
				.Inject(this)
				.Add(new MonsterSystem())
				.Add(new CharacterSystem())
				.Add(new PlayerSystem());

			ecsSystems.Init();

            if (DataManager.TryGetConfigValue("SingleMobTest", out var mobName))
            {
                var mobCount = 1;
                if (DataManager.TryGetConfigInt("SingleMobCount", out var c))
                    mobCount = c;

                DataManager.DoSingleMobTest(mobName, mobCount);
            }

            var maps = DataManager.Maps;

			var entities = 0;
			
			for (var j = 0; j < maps.Count; j++)
			{
				var mapData = maps[j];
				try
				{
					var map = new Map(this, mapData.Code, mapData.WalkData);
					map.Id = j;

					mapIdLookup.Add(mapData.Code, j);

					var spawns = DataManager.GetSpawnsForMap(mapData.Code);

					if (spawns != null)
					{
						for (var i = 0; i < spawns.Count; i++)
						{
							var s = spawns[i];
							var mobId = DataManager.GetMonsterIdForCode(s.Class);

							for (var k = 0; k < s.Count; k++)
							{
								var m = CreateMonster(map, mobId, s.X, s.Y, s.Width, s.Height, s);
								if (!m.IsNull())
								{
									map.AddEntity(ref m);
									entities++;
								}
							}
						}
					}

					var connectors = DataManager.GetMapConnectors(mapData.Code);

					if (connectors != null)
					{
						for (var i = 0; i < connectors.Count; i++)
						{
							var c = connectors[i];
							var mobId = 1000;

							var m = CreateMonster(map, mobId, c.SrcArea.MidX, c.SrcArea.MidY, 0, 0, null);
							if (!m.IsNull())
								map.AddEntity(ref m);
						}
					}

					Maps.Add(map);
				}
				catch (Exception e)
				{
					ServerLogger.LogError($"Failed to load map {mapData.Name} ({mapData.Code}) due to error while loading: {e.Message}");
				}
			}

			mapCount = maps.Count;

			ServerLogger.Log($"World started with {entities} entities.");
		}

		public void RunEcs()
		{
			ecsSystems.Run();
		}

		public void Update()
		{
			for (var i = 0; i < mapCount; i++)
				Maps[i].Update();

			PerformRemovals();

			if (CommandBuilder.HasRecipients())
				ServerLogger.LogWarning("Command builder has recipients after completing server update loop!");
		}

		public bool RespawnMonster(Monster monster)
		{
			if (monster.Character.IsActive)
				return false;

			var spawnEntry = monster.SpawnEntry;
			var x = spawnEntry.X;
			var y = spawnEntry.Y;
			var width = spawnEntry.Width;
			var height = spawnEntry.Height;
			var map = Maps[mapIdLookup[monster.SpawnMap]];

			var area = Area.CreateAroundPoint(new Position(x, y), width, height);
			area.ClipArea(map.MapBounds);

			Position p;
			if (width == 0 && height == 0 && x != 0 && y != 0)
			{
				p = new Position(x, y);
			}
			else
			{
				if (x == 0 && y == 0 && width == 0 && height == 0)
					area = map.MapBounds;

				if (!map.FindPositionInRange(area, out p))
				{
					return false;
				}
			}

			var ch = monster.Character;
			var ce = monster.CombatEntity;
			var e = monster.Entity;

			ch.IsActive = true;
			ch.Map = map;
			ch.Position = p;
			ch.AttackCooldown = 0f;
			ch.State = CharacterState.Idle;
			
			ce.Init(ref e, ch);
			monster.Initialize(ref e, ch, ce, monster.MonsterBase, monster.MonsterBase.AiType, spawnEntry, map.Name);

			map.AddEntity(ref e);

			return true;
		}

		public EcsEntity CreateMonster(Map map, int classId, int x, int y, int width, int height, MapSpawnEntry spawnEntry)
		{
			var e = ecsWorld.AddAndReset<Character, CombatEntity, Monster>(
				out var ch, out var ce, out var m);

			var area = Area.CreateAroundPoint(new Position(x, y), width, height);
			area.ClipArea(map.MapBounds);

			Position p;
			if (width == 0 && height == 0 && x != 0 && y != 0)
			{
				p = new Position(x, y);
			}
			else
			{
				if (x == 0 && y == 0 && width == 0 && height == 0)
					area = map.MapBounds;

				if (!map.FindPositionInRange(area, out p))
				{
					ServerLogger.LogWarning($"Failed to spawn {classId} on map {map.Name}, could not find spawn location around {x},{y}. Spawning randomly on map.");
					map.FindPositionInRange(map.MapBounds, out p);
				}
			}

			var mon = DataManager.GetMonsterById(classId);
			ch.Id = GetNextEntityId();
			ch.ClassId = classId;
			ch.Entity = e;
			ch.Position = p;
			ch.MoveSpeed = mon.MoveSpeed;
			ch.Type = CharacterType.Monster;
			ch.FacingDirection = (Direction)GameRandom.Next(0, 7);

			ce.Entity = e;

			entityList.Add(ch.Id, e);

			ce.Init(ref e, ch);
			m.Initialize(ref e, ch, ce, mon, mon.AiType, spawnEntry, map.Name);

			//ServerLogger.Log("Entity spawned at position: " + ch.Position);

			return e;
		}

		public EcsEntity CreatePlayer(NetworkConnection connection, string mapName, Area spawnArea)
		{
			var e = ecsWorld.AddAndReset<Character, CombatEntity, Player>(
				out var ch, out var ce, out var player);

			var mapId = mapIdLookup[mapName];

			var map = Maps[mapId];

			if (spawnArea.IsZero)
				spawnArea = map.MapBounds;

			Position p;

			if (spawnArea.Width > 1 || spawnArea.Height > 1)
			{
				p = spawnArea.RandomInArea();

				//Position p = new Position(170 + GameRandom.Next(-5, 5), 365 + GameRandom.Next(-5, 5));
				var attempt = 0;
				do
				{
					attempt++;
					if (attempt > 100)
					{
						ServerLogger.LogWarning("Trouble spawning player, will place him on a random cell instead.");
						spawnArea = map.MapBounds;
						attempt = 0;
					}

					p = spawnArea.RandomInArea();
				} while (!map.WalkData.IsCellWalkable(p));
			}
			else
				p = new Position(spawnArea.MidX, spawnArea.MidY);

			ch.Id = GetNextEntityId();
			ch.IsActive = false; //start off inactive

			ch.Entity = e;
			ch.Position = p;
			ch.ClassId = 0; // GameRandom.Next(0, 6);
			ch.MoveSpeed = 0.15f;
			ch.Type = CharacterType.Player;
			ch.FacingDirection = (Direction)GameRandom.Next(0, 7);

			ce.Init(ref e, ch);

            ce.BaseStats.Level = 1;
			
			player.Connection = connection;
			player.Entity = e;
			player.CombatEntity = ce;
			player.Character = ch;
			player.IsMale = GameRandom.Next(0, 1) == 0;
			player.HeadId = (byte)GameRandom.Next(0, 28);
            player.Name = "Player " + GameRandom.Next(0, 999);

			player.Init();

			connection.Player = player;


			entityList.Add(ch.Id, e);
			//player.IsMale = false;

			//player.IsMale = true;
			//ch.ClassId = 1;
			//player.HeadId = 15;

			//map.SendAllEntitiesToPlayer(ref e);
			map.AddEntity(ref e);


			return e;
		}

		public EcsEntity GetEntityById(int id)
		{
			if (entityList.TryGetValue(id, out var entity))
				return entity;

			return EcsEntity.Null;
		}

        public void PerformRemovals()
        {
            for (var i = 0; i < removeList.Count; i++)
            {
                var entity = removeList[i];

                if (entity.IsNull() || !entity.IsAlive())
                    return;

				ServerLogger.Debug($"Removing entity {entity} from world.");
                var player = entity.Get<Player>();
                var combatant = entity.Get<CombatEntity>();
                var monster = entity.Get<Monster>();
                var ch = entity.Get<Character>();

				player?.Reset();
                combatant?.Reset();
                ch?.Reset();
                monster?.Reset();

                entity.Destroy();
			}

			removeList.Clear();
        }
		
		public void RemoveEntity(ref EcsEntity entity)
        {
            //remove immediately from world, queue entity destruction to happen after we finish looping everything
			removeList.Add(entity);

            var ch = entity.Get<Character>();

			if (ch != null)
            {
                entityList.Remove(ch.Id);
                ch.Map?.RemoveEntity(ref entity, CharacterRemovalReason.OutOfSight);
                ch.IsActive = false;
            }
		}

        public bool MapExists(string mapName)
        {
            return mapIdLookup.ContainsKey(mapName);
        }

        public bool TryGetMapByName(string mapName, out Map map)
        {
            if (mapIdLookup.TryGetValue(mapName, out var mapId))
            {
                map = Maps[mapId];
                return true;
            }

            map = null;
            return false;
        }

		public void MovePlayerMap(ref EcsEntity entity, Character character, string mapName, Position newPosition)
		{
			character.IsActive = false;
			character.Map.RemoveEntity(ref entity, CharacterRemovalReason.OutOfSight);

			character.ResetState();
			character.Position = newPosition;

			if (!mapIdLookup.TryGetValue(mapName, out var mapId))
			{
				ServerLogger.LogWarning($"Map {mapName} does not exist! Could not move player.");
				return;
			}

			var map = Maps[mapId];


			if (newPosition == Position.Zero)
			{
				map.FindPositionInRange(map.MapBounds, out var p);
				character.Position = newPosition;
			}


			map.AddEntity(ref entity);

			var player = entity.Get<Player>();
			player.Connection.LastKeepAlive = Time.ElapsedTime; //reset tick time so they get 2 mins to load the map

			CommandBuilder.SendChangeMap(character, player);
		}

		private int NextPowerOf2(int n)
		{
			n--;
			n |= n >> 1;
			n |= n >> 2;
			n |= n >> 4;
			n |= n >> 8;
			n |= n >> 16;
			n++;
			return n;
		}

		private int GetNextEntityId()
		{
			nextEntityId++;
			if (nextEntityId > maxEntityId)
				nextEntityId = 0;

			if (!entityList.ContainsKey(nextEntityId))
				return nextEntityId;

			while (entityList.ContainsKey(nextEntityId))
				nextEntityId++;

			return nextEntityId;
		}
	}
}
