using System.Numerics;
using System.Runtime.CompilerServices;
using RebuildSharedData.Enum;

namespace RebuildSharedData.Data;

public struct Position : IEquatable<Position>
{
    public int X;
    public int Y;

    public int Width => X;
    public int Height => Y;

    public static Position Zero => new Position(0, 0);
    public static Position Invalid => new Position(-999, -999);

    public int PackIntoInt => X + (Y << 12);
    public static Position UnpackIntPosition(int pos) => new Position(pos & 0xFFF, pos >> 12);
    
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

    public bool IsValid() => X > 0 && Y > 0;
    public Position Abs() => new(X < 0 ? X * -1 : X, Y < 0 ? Y * -1 : Y);


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

    /// <summary>
    /// Calculate the chebyshev distance to the destination.
    /// A simplified distance check that returns the max distance along both the x and y-axes.
    /// Used to determine if a point is within a square area.
    /// I realize this probably will cause confusion with squared distance...
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int SquareDistance(Position dest) => Math.Max(Math.Abs(X - dest.X), Math.Abs(Y - dest.Y));

    /// <summary>
    /// Returns the sum of the distance on both the X and Y axis.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int BlockDistance(Position dest) => Math.Abs(X - dest.X) + Math.Abs(Y - dest.Y);

    public float Angle(Position b)
    {
        float xDiff = b.X - X;
        float yDiff = b.Y - Y;
        var angle = (float)((float)Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI) - 90;
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


    public static Position RandomPosition(Position position, int distance)
    {
        return RandomPosition(position.X-distance, position.Y-distance, position.X+distance, position.Y+distance);
    }

    public static Position RandomPosition(int maxx, int maxy)
    {
        var x = GameRandom.NextInclusive(0, maxx);
        var y = GameRandom.NextInclusive(0, maxy);
        return new Position(x, y);
    }

    public static Position RandomPosition(int minx, int miny, int maxx, int maxy)
    {
        var x = GameRandom.NextInclusive(minx, maxx);
        var y = GameRandom.NextInclusive(miny, maxy);
        return new Position(x, y);
    }

    public void ClampToArea(Area bounds)
    {
        if(X < bounds.MinX)
            X = bounds.MinX;
        if(X  > bounds.MaxX)
            X = bounds.MaxX;
        if(Y < bounds.MinY)
            Y = bounds.MinY;
        if(Y  > bounds.MaxY)
            Y = bounds.MaxY;
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
        if (X < -1 || X > 1 || Y < -1 || Y > 1)
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
    
    public Position Normalize()
    {
        var x = X;
        var y = Y;
        if (x < -1) x = -1;
        if (x > 1) x = 1;
        if (y < -1) y = -1;
        if (y > 1) y = 1;

        return new Position(x, y);
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
            return X + Y * 4096;
        }
    }

    public override string ToString()
    {
        return $"{X.ToString()},{Y.ToString()}";
    }

    public static Position operator -(Position left, Position right) => new Position(left.X - right.X, left.Y - right.Y);
    public static Position operator +(Position left, Position right) => new Position(left.X + right.X, left.Y + right.Y);
    public static Position operator /(Position left, int right) => new Position(left.X / right, left.Y / right);

    public static Position operator *(Position v, int scalar) => new Position(v.X * scalar, v.Y * scalar);
    public static Position operator *(int scalar, Position v) => new Position(v.X * scalar, v.Y * scalar);
}