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
	[Flags]
    public enum CellType
	{
		None = 0,
		Walkable = 1,
		Water = 2,
		Snipable = 4,
		SeeThrough = Walkable | Snipable,
	}

	public class MapWalkData
	{
		public int Width;
		public int Height;
        public Area Bounds;
		private byte[] cellData;
		
        public bool IsPositionInBounds(Position p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
        public bool IsPositionInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
        public bool IsCellWalkable(int x, int y) => (cellData[x + y * Width] & 1) == 1;
		public bool IsCellWalkable(Position p) => (cellData[p.X + p.Y * Width] & 1) == 1;
		public bool IsCellSnipable(int x, int y) => (cellData[x + y * Width] & 4) == 4;
		public bool IsCellSnipable(Position p) => (cellData[p.X + p.Y * Width] & 4) == 4;
        public bool DoesCellBlockLos(int x, int y) => (cellData[x + y * Width] & (byte)CellType.SeeThrough) == 0;
        public bool DoesCellBlockLos(Position p) => (cellData[p.X + p.Y * Width] & (byte)CellType.SeeThrough) == 0;
        
        public Position FindWalkdableCellOnMap(int tries)
        {
            var pos = Position.Invalid;
            var count = 0;

            do
            {
                if (count > tries)
                    break;
                pos = new Position(GameRandom.Next(0, Width - 1), GameRandom.Next(0, Height - 1));
                count++;
            } while (!IsCellWalkable(pos));

            return pos;
        }

        public Position FindWalkableCellOnMap()
        {
            //we assume we will find a walkable cell on a map, so we have no max attempts specified.
            //If we have a map with no of few walkable cells this could hard lock the server however.

            var pos = Position.Invalid; 

            do
            {
                pos = new Position(GameRandom.Next(0, Width - 1), GameRandom.Next(0, Height - 1));
            } while (!IsCellWalkable(pos));

            return pos;
        }
        
        public bool FindWalkableCellInArea(Area area, out Position p, int maxAttempts = 100)
        {
            p = new Position();

            area = area.ClipAndNormalize(Bounds);

            var attempt = 0;

            do
            {
                p = new Position(GameRandom.Next(area.MinX, area.MaxX), GameRandom.Next(area.MinY, area.MaxY));
                if (attempt > maxAttempts)
                    return false;
                attempt++;
            } while (!IsCellWalkable(p));

            return true;
		}

        /// <summary>
        /// Starting from a random point within an area, scans tiles one at a time until a walkable tile is found.
        /// </summary>
        /// <param name="area">The area you want to find a walkable cell in.</param>
        /// <param name="p">The output area.</param>
        /// <returns>If a walkable tile was found.</returns>
        public bool ScanForWalkableCell(Area area, out Position p)
        {
            area = area.ClipAndNormalize(Bounds);

            var max = area.Width * area.Height;
            var start = GameRandom.Next(0, max - 1);
            var pos = start+1;

            while (start != pos)
            {
                pos = pos % max;

                var x = pos % area.Width;
                var y = (pos - x) / area.Height;

                if (IsCellWalkable(x, y))
                {
                    p = new Position(x, y);
                    return true;
                }

                pos++;
            }

            p = Position.Invalid;
            return false;
        }

        public bool HasLineOfSight(Position pos1, Position pos2)
        {
            var x0 = pos1.X;
            var y0 = pos1.Y;
            var x1 = pos2.X;
            var y1 = pos2.Y;

			//algorithm from https://rosettacode.org/wiki/Bitmap/Bresenham%27s_line_algorithm#C.23
			int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = (dx > dy ? dx : -dy) / 2;
            for (; ; )
            {
                if (x0 == x1 && y0 == y1) break;
                if (DoesCellBlockLos(x0, y0))
                    return false;
				var e2 = err;
                if (e2 > -dx) { err -= dy; x0 += sx; }
                if (e2 < dy) { err += dx; y0 += sy; }
            }

            return true;
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

                Bounds = new Area(0, 0, Width-1, Height-1);

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
