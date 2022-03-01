using System;
using System.Collections.Generic;
using System.Text;
using RebuildData.Server.Config;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;

namespace RebuildData.Server.Pathfinding
{
    public static class DistanceCache
	{
		private static Direction[] directions;
		private static float[] angles;
		private static float[] distances;
		private static int[] intDistances;

		private static int max;
		private static int width;
		private static int height;

		private const int center = ServerConfig.MaxViewDistance;
		
		public static void Init()
		{
			max = ServerConfig.MaxViewDistance;
			width = max * 2 + 1;
			height = max * 2 + 1;

			angles = new float[width * height];
			distances = new float[width * height];
			intDistances = new int[width * height];
			directions = new Direction[width * height];

			var centerPos = new Position(center, center);

			for (var x = 0; x < width; x++)
			{
				for (var y = 0; y < height; y++)
				{
					var pos = new Position(x, y);

					distances[x + y * width] = CalcDistance(x, y, center, center);
					intDistances[x + y * width] = (int)Math.Round(distances[x + y * width]);

					var angle = centerPos.Angle(pos);
					var facing = Directions.GetFacingForAngle(angle);

					angles[x + y * width] = angle;
					directions[x + y * width] = facing;
				}
			}
		}

		public static float Angle(Position p1, Position p2)
		{
			var offset = p1 - p2;
			if (offset.SquareDistance(Position.Zero) > max)
				return p1.Angle(p2);

			return angles[(offset.X + center) + (offset.Y + center) * width];
		}

		public static Direction Direction(Position p1, Position p2)
		{
			var offset = p2 - p1;
			if (offset.SquareDistance(Position.Zero) > max)
			{
				var angle = p1.Angle(p2);
				return Directions.GetFacingForAngle(angle);
			}

			return directions[(offset.X + center) + (offset.Y + center) * width];
		}

		public static int IntDistance(Position p1, Position p2)
		{
			var offset = p1 - p2;
			if (offset.SquareDistance(Position.Zero) > max)
				return (int)Math.Round(CalcDistance(offset.X, offset.Y, 0, 0));

			return intDistances[(offset.X + center) + (offset.Y + center) * width];
		}

		public static float Distance(Position p1, Position p2)
		{
			var offset = p1 - p2;
			if (offset.SquareDistance(Position.Zero) > max)
				return CalcDistance(offset.X, offset.Y, 0, 0);

			return distances[(offset.X + center) + (offset.Y + center) * width];
		}

		private static float CalcDistance(int x1, int y1, int x2, int y2)
		{
			var p1 = Math.Pow((x2 - x1), 2);
			var p2 = Math.Pow((y2 - y1), 2);
			return (float)Math.Sqrt(p1 + p2);
		}

        public static int DistanceTo(this Position p1, Position p2)
        {
            return IntDistance(p1, p2);
        }


        public static bool InRange(this Position p1, Position p2, int distance)
        {
            return IntDistance(p1, p2) <= distance;
        }
	}
	
}
