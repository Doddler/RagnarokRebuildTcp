using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Server.Data.CsvDataTypes
{
	public class CsvMapSpawnEntry
	{
		public string Map { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public int Count { get; set; }
		public string Class { get; set; }
		public int SpawnTime { get; set; }
		public int Variance { get; set; }
		public int Recycle { get; set; }
	}
}
