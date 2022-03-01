using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Server.Data.CsvDataTypes
{
	internal class CsvMapConnector
	{
		public bool Enabled { get; set; }
		public string Source { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public string Target { get; set; }
		public int TargetX { get; set; }
		public int TargetY { get; set; }
		public int TargetWidth { get; set; }
		public int TargetHeight { get; set; }
	}
}
