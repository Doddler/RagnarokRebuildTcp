using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Shared.ClientTypes
{
	[Serializable]
	public class ClientMapEntry
	{
		public string Code;
		public string Name;
		public string Music;
	}

	[Serializable]
	public class ClientMapList
	{
		public List<ClientMapEntry> MapEntries;
	}
}
