using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RebuildData.Server.Config;
using RebuildData.Server.Data;
using RebuildData.Server.Logging;
using RebuildZoneServer.Data.Management;

namespace RebuildData.Shared.Data
{
	public enum CellType
	{
		None = 0,
		Walkable = 1,
		Water = 2,
		Snipable = 4
	}

	public class MapWalkData
	{
		public int Width;
		public int Height;
		private byte[] cellData;

        public bool IsPositionInBounds(Position p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
        public bool IsPositionInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
        public bool IsCellWalkable(int x, int y) => (cellData[x + y * Width] & 1) == 1;
		public bool IsCellWalkable(Position p) => (cellData[p.X + p.Y * Width] & 1) == 1;
		public bool IsCellSnipable(int x, int y) => (cellData[x + y * Width] & 2) == 2;
		public bool IsCellSnipable(Position p) => (cellData[p.X + p.Y * Width] & 2) == 2;

        public Position FindWalkdableCellOnMap()
        {
            var pos = Position.Invalid;
			
            do
            {
                pos = new Position(GameRandom.Next(0, Width - 1), GameRandom.Next(0, Height - 1));
            } while (!IsCellWalkable(pos));

            return pos;
        }
		

		public MapWalkData(string name)
		{
			if(!DataManager.TryGetConfigValue("WalkPathData", out var walkPath))
				throw new Exception("Configuration did not have section for WalkPathData!");

			var path = Path.Combine(walkPath, name);

			
			//ServerLogger.Log("Loading path data from " + name);

			try
			{
				using var fs = new FileStream(path, FileMode.Open);
				using var br = new BinaryReader(fs);

				Width = br.ReadInt32();
				Height = br.ReadInt32();

				cellData = br.ReadBytes(Width * Height);
			}
			catch (Exception)
			{
				ServerLogger.LogError($"Failed to load map data for file {name}");
				throw;
			}
		}
	}
}
