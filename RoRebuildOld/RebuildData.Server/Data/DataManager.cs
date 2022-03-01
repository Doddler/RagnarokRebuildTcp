using System;
using System.Collections.Generic;
using RebuildData.Server.Data;
using RebuildData.Server.Data.Monster;
using RebuildData.Server.Data.Types;
using RebuildData.Server.Logging;
using RebuildData.Shared.Data;
using RebuildZoneServer.Data.Management.Types;

namespace RebuildZoneServer.Data.Management
{
	public static class DataManager
	{
		private static List<MonsterDatabaseInfo> monsterStats;
		private static Dictionary<int, MonsterDatabaseInfo> monsterIdLookup;
		private static Dictionary<string, MonsterDatabaseInfo> monsterCodeLookup;
		private static Dictionary<string, List<MapConnector>> mapConnectorLookup;

		private static List<List<MonsterAiEntry>> monsterAiList;

		private static Dictionary<string, string> configValues;

		private static MapSpawnDatabaseInfo mapSpawnInfo;

		private static List<MapEntry> mapList;
		public static List<MapEntry> Maps => mapList;

        public static ExpChart ExpChart;

		public static bool HasMonsterWithId(int id)
		{
			return monsterIdLookup.ContainsKey(id);
		}

		public static List<MonsterAiEntry> GetAiStateMachine(MonsterAiType monsterType)
		{
			return monsterAiList[(int)monsterType];
		}

		public static bool TryGetConfigValue(string key, out string value)
		{
			if (configValues.TryGetValue(key, out value))
				return true;

			value = null;
			return false;
		}

		public static bool TryGetConfigInt(string key, out int value)
		{
			if (configValues.TryGetValue(key, out var val))
			{
				if (int.TryParse(val, out value))
					return true;
			}

			value = 0;
			return false;
		}

		public static List<MapConnector> GetMapConnectors(string mapName)
		{
			if (mapConnectorLookup.TryGetValue(mapName, out var list))
				return list;

			mapConnectorLookup.Add(mapName, new List<MapConnector>());
			return mapConnectorLookup[mapName];
		}

		public static int GetMonsterIdForCode(string code)
		{
#if DEBUG
			if (!monsterCodeLookup.ContainsKey(code))
				throw new Exception("Could not find monster in code lookup with with code: " + code);
#endif
			return monsterCodeLookup[code].Id;
		}

		public static MonsterDatabaseInfo GetMonsterById(int id)
		{
			return monsterIdLookup[id];
		}

		public static List<MapSpawnEntry> GetSpawnsForMap(string mapCode)
		{
			if (mapSpawnInfo.MapSpawnEntries.ContainsKey(mapCode))
				return mapSpawnInfo.MapSpawnEntries[mapCode];

			return null;
		}

		public static MapConnector GetConnector(string mapName, Position pos)
		{
			if (!mapConnectorLookup.ContainsKey(mapName))
				return null;

			var cons = mapConnectorLookup[mapName];
			for (var i = 0; i < mapConnectorLookup[mapName].Count; i++)
			{
				var entry = mapConnectorLookup[mapName][i];

				if (entry.SrcArea.Contains(pos))
					return entry;
			}

			return null;
		}

		public static void DoSingleMobTest(string mobName, int count)
		{
			mapList.RemoveAll(m => m.Code != "2009rwc_03");
			mapSpawnInfo.MapSpawnEntries = new Dictionary<string, List<MapSpawnEntry>>();
			mapSpawnInfo.MapSpawnEntries.Add("2009rwc_03", new List<MapSpawnEntry>()
			{
				new MapSpawnEntry()
				{
					Class = mobName,
					Count = count,
					Height = 2,
					Width = 2,
					X = 50,
					Y = 50,
					SpawnTime = 0,
					SpawnVariance = 0
				}
			});
		}

		public static void Initialize()
		{
			var loader = new DataLoader();

			configValues = loader.LoadServerConfig();
			mapList = loader.LoadMaps();
			mapConnectorLookup = loader.LoadConnectors(mapList);
			monsterStats = loader.LoadMonsterStats();
			mapSpawnInfo = loader.LoadSpawnInfo();
			monsterAiList = loader.LoadAiStateMachines();
            ExpChart = loader.LoadExpChart();
			
			monsterIdLookup = new Dictionary<int, MonsterDatabaseInfo>(monsterStats.Count);
			monsterCodeLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);
			
			foreach (var m in monsterStats)
			{
				monsterIdLookup.Add(m.Id, m);
				monsterCodeLookup.Add(m.Code, m);
			}
		}
	}
}
