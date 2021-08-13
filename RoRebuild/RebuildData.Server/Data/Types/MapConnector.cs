using System;
using System.Collections.Generic;
using System.Text;
using RebuildData.Shared.Data;

namespace RebuildZoneServer.Data.Management.Types
{
	public class MapConnector
	{
		public string Map { get; set; }
		public Area SrcArea { get; set; }
		public string Target { get; set; }
		public Area DstArea { get; set; }
	}
}
