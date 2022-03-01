using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildData.Server.Pathfinding
{
	public class PathData
	{
		public int Id;
		public byte[] Path;
		public int Length;

		public const int MaxPathLength = 16;

		public PathData(int id)
		{
			Id = id;
			Path = new byte[MaxPathLength];
			Length = 0;
		}

		public void Reset()
		{
			Length = 0;
		}
	}
}
