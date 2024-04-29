using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.Utility
{
	public static class Vector2IntExtensions
	{
		public static int SquareDistance(this Vector2Int v)
		{
			return Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));
		}


		public static Vector2Int GetVectorValue(this Direction dir)
		{
			switch (dir)
			{
				case Direction.SouthWest: return new Vector2Int(-1, -1);
				case Direction.West: return new Vector2Int(-1, 0);
				case Direction.NorthWest: return new Vector2Int(-1, 1);
				case Direction.North: return new Vector2Int(0, 1);
				case Direction.NorthEast: return new Vector2Int(1, 1);
				case Direction.East: return new Vector2Int(1, 0);
				case Direction.SouthEast: return new Vector2Int(1, -1);
				case Direction.South: return new Vector2Int(0, -1);
			}

			return Vector2Int.zero;
		}

		public static Vector2Int AddDirection(this Vector2Int v, Direction d)
		{
			switch (d)
			{
				case Direction.SouthWest: return new Vector2Int(v.x - 1, v.y - 1);
				case Direction.West: return new Vector2Int(v.x - 1, v.y);
				case Direction.NorthWest: return new Vector2Int(v.x - 1, v.y + 1);
				case Direction.North: return new Vector2Int(v.x, v.y + 1);
				case Direction.NorthEast: return new Vector2Int(v.x + 1, v.y + 1);
				case Direction.East: return new Vector2Int(v.x + 1, v.y);
				case Direction.SouthEast: return new Vector2Int(v.x + 1, v.y - 1);
				case Direction.South: return new Vector2Int(v.x, v.y - 1);
			}

			return v;
		}
	}
}
