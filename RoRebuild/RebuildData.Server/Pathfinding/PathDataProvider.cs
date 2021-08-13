using System;
using System.Collections.Generic;
using System.Text;
using RebuildData.Shared.Util;

namespace RebuildData.Server.Pathfinding
{
	public static class PathDataProvider
	{
		public static PathData[] pathData;

		public const int MaxPathCache = 300;

		private static EasyStack<int> freeEntries;

		static PathDataProvider()
		{
			pathData = new PathData[MaxPathCache];
			freeEntries = new EasyStack<int>(MaxPathCache);
			for (var i = 0; i < MaxPathCache; i++)
			{
				pathData[i] = new PathData(i);
				freeEntries.Add(i);
			}
		}

		static PathData Take()
		{
			var id = freeEntries.Take();
			pathData[id].Reset();
			return pathData[id];
		}

		static void Return(PathData path)
		{
			freeEntries.Add(path.Id);
		}
	}
}
