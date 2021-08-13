using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Server.Data.Types
{
	class MapSpawnDatabaseInfo
	{
		public Dictionary<string, List<MapSpawnEntry>> MapSpawnEntries;
	}

	public class MapSpawnEntry
	{
		public int X;
		public int Y;
		public int Width;
		public int Height;
		public int Count;
		public string Class;
		public float SpawnTime;
		public float SpawnVariance;
	}
}
