using System;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using RebuildData.Shared.Enum;

namespace RebuildData.Shared.Data
{
	public struct Position : IEquatable<Position>
	{
		public int X;
		public int Y;

		public int Width => X;
		public int Height => Y;

		public static Position Zero => new Position(0, 0);
		public static Position Invalid => new Position(-999, -999);

        public Position(int x, int y)
		{
			X = x;
			Y = y;
		}
		
		public Position(Position src)
		{
			X = src.X;
			Y = src.Y;
		}

        public bool IsValid() => X >= 0 && Y >= 0;

        
		public Position StepTowards(Position dest)
		{
			var pos = new Position(this);

			if (pos.X < dest.X)
				pos.X++;
			if (pos.X > dest.X)
				pos.X--;
			if (pos.Y < dest.Y)
				pos.Y++;
			if (pos.Y > dest.Y)
				pos.Y--;

			return pos;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int SquareDistance(Position dest)
		{
			return Math.Max(Math.Abs(X - dest.X), Math.Abs(Y - dest.Y));
		}

		public float Angle(Position b)
		{
			float xDiff = b.X - X;
			float yDiff = b.Y - Y;
			var angle = (float) ((float) Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI) - 90;
			if (angle < -180)
				angle += 360;
			
			return angle;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool InRange(Position target, int distance)
		{
			return SquareDistance(target) <= distance;
			//return target.X >= X - distance && target.X <= X + distance && target.Y >= Y - distance && target.Y <= Y + distance;
		}

		public static Position RandomPosition(Area area)
		{
			return RandomPosition(area.MinX, area.MinY, area.MaxX, area.MaxY);
		}

		public static Position RandomPosition(int maxx, int maxy)
		{
			var x = GameRandom.Next(0, maxx);
			var y = GameRandom.Next(0, maxy);
			return new Position(x, y);
		}

		public static Position RandomPosition(int minx, int miny, int maxx, int maxy)
		{
			var x = GameRandom.Next(minx, maxx);
			var y = GameRandom.Next(miny, maxy);
			return new Position(x, y);
		}


		public float GetDirection()
		{
			var rad = Math.Atan2(X, Y);
			
			var deg = rad * (180 / Math.PI);
			return (float)deg;

			//return Direction.South;
		}

		public bool IsOffsetDiagonal()
		{
#if DEBUG
			//sanity check
			if (X < -1 || X > 1 || Y < -1 || Y > 1)
				throw new Exception("IsOffsetDiagonal provided invalid inputs!");
#endif
			if (Math.Abs(X) != 0 && Math.Abs(Y) != 0)
				return true;

			return false;
		}

		public Direction GetDirectionForOffset()
		{
#if DEBUG
			//sanity check
			if(X < -1 || X > 1 || Y < -1 || Y > 1)
				throw new Exception("Get Direction provided invalid inputs!");
#endif

			if (X == -1 && Y == -1) return Direction.SouthWest;
			if (X == -1 && Y == 0) return Direction.West;
			if (X == -1 && Y == 1) return Direction.NorthWest;
			if (X == 0 && Y == 1) return Direction.North;
			if (X == 1 && Y == 1) return Direction.NorthEast;
			if (X == 1 && Y == 0) return Direction.East;
			if (X == 1 && Y == -1) return Direction.SouthEast;
			if (X == 0 && Y == -1) return Direction.South;

			return Direction.South;
		}

		public Position AddDirectionToPosition(Direction d)
		{
			switch (d)
			{
				case Direction.SouthWest: return new Position(X - 1, Y - 1);
				case Direction.West: return new Position(X - 1, Y);
				case Direction.NorthWest: return new Position(X - 1, Y + 1);
				case Direction.North: return new Position(X, Y + 1);
				case Direction.NorthEast: return new Position(X + 1, Y + 1);
				case Direction.East: return new Position(X + 1, Y);
				case Direction.SouthEast: return new Position(X + 1, Y - 1);
				case Direction.South: return new Position(X, Y - 1);
			}

			return this;
		}

		public static bool operator ==(Position src, Position dest)
		{
			return src.X == dest.X && src.Y == dest.Y;
		}

		public static bool operator !=(Position src, Position dest)
		{
			return src.X != dest.X || src.Y != dest.Y;
		}

		public bool Equals(Position other)
		{
			return X == other.X && Y == other.Y;
		}

		public override bool Equals(object obj)
		{
			return obj is Position other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (X * 397) ^ Y;
			}
		}

		public override string ToString()
		{
			return $"{X.ToString()},{Y.ToString()}";
		}

		public static Position operator - (Position left, Position right) => new Position(left.X - right.X, left.Y - right.Y);
		public static Position operator + (Position left, Position right) => new Position(left.X + right.X, left.Y + right.Y);
		public static Position operator / (Position left, int right) => new Position(left.X / right, left.Y / right);
	}
}
