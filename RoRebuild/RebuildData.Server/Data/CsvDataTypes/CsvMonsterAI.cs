using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Server.Data.CsvDataTypes
{
	internal class CsvMonsterAI
	{
		public string AiType { get; set; }
		public string State { get; set; }
		public string InputCheck { get; set; }
		public string OutputCheck { get; set; }
		public string EndState { get; set; }
	}
}
