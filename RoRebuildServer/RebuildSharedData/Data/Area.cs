using RebuildSharedData.Enum;

namespace RebuildSharedData.Data;

public struct Area
{
    public int MinX;
    public int MinY;
    public int MaxX;
    public int MaxY;

    public Area Clone => new Area(this);
    public static Area Zero => new Area(0, 0, 0, 0);

    public bool IsZero => MinX == 0 && MaxX == 0 && MinY == 0 && MaxY == 0;
    public bool IsSingleCell => Width <= 1 && Height <= 1;

    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;

    public int MidX => MinX + (MaxX - MinX) / 2;
    public int MidY => MinY + (MaxY - MinY) / 2;

    public int Size => Width * Height;

    public Position Min => new Position(MinX, MinY);
    public Position Center => new Position(MinX + (Width / 2), MinY + (Height / 2));
    public Position Max => new Position(MaxX, MaxY);

    //public static Area AreaAroundPoint(int x, int y, int w, int h) => new Area(x - w, y - h, x + w, y + h);

    public Area(int minX, int minY, int maxX, int maxY)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    public Area(Area src)
    {
        MinX = src.MinX;
        MinY = src.MinY;
        MaxX = src.MaxX;
        MaxY = src.MaxY;
    }


    public bool Contains(Position p)
    {
        return p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;
    }

    public bool PointInArea(int x, int y)
    {
        return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    public bool Overlaps(Area b)
    {
        //if one rectangle is on the left side of the other
        if (MinX > b.MaxX || b.MinX > MaxX)
            return false;

        //if one rectangle is above the other
        if (MinY > b.MaxY || b.MinY > MaxY)
            return false;

        return true;
    }

    public bool HasEntered(Position initial, Position dest)
    {
        if (Contains(initial))
            return false;

        return Contains(dest);
    }

    public Area Shrink(int x, int y)
    {
        if (x > Width)
            x = Width;
        if (y > Height)
            y = Height;
        return new Area(MinX + x, MinY + y, MaxX - x, MaxY - y).Normalize();
    }

    public Position RandomInArea()
    {
        return new Position(GameRandom.NextInclusive(MinX, MaxX), GameRandom.NextInclusive(MinY, MaxY));
    }

    public Direction DirectionFromArea(int x, int y)
    {
        if (x < MinX)
        {
            if (y > MaxY)
                return Direction.NorthWest;
            if (y < MinY)
                return Direction.SouthWest;
            return Direction.West;
        }

        if (x > MaxX)
        {
            if (y > MaxY)
                return Direction.NorthEast;
            if (y < MinY)
                return Direction.SouthEast;
            return Direction.East;
        }

        if (y > MaxY)
            return Direction.North;
        if (y < MinY)
            return Direction.South;

        return Direction.None;
    }

    public Area ClipArea(Area clippingArea)
    {
        if (MinX < clippingArea.MinX)
            MinX = clippingArea.MinX;
        if (MinY < clippingArea.MinY)
            MinY = clippingArea.MinY;
        if (MaxX > clippingArea.MaxX)
            MaxX = clippingArea.MaxX;
        if (MaxY > clippingArea.MaxY)
            MaxY = clippingArea.MaxY;

        if (MinX > MaxX)
            MinX = MaxX;
        if (MaxX < MinX)
            MaxX = MinX;
        if (MinY > MaxY)
            MinY = MaxY;
        if (MaxY < MinY)
            MaxY = MinY;

        return this;
    }

    public Area ClipAndNormalize(Area clippingArea)
    {
        clippingArea.Normalize();
        return ClipArea(clippingArea);
    }

    /// <summary>
    /// Adjusts the area in case the min and max bounds are flipped.
    /// </summary>
    public Area Normalize()
    {
        if (MinX > MaxX || MaxX < MinX)
        {
            var minx = MinX;
            MinX = MaxX;
            MaxX = minx;
        }

        if (MinY > MaxY || MaxY < MinY)
        {
            var miny = MinY;
            MinY = MaxY;
            MaxY = miny;
        }

        return this;
    }

    public static Area CreateAroundTwoPoints(Position a, Position b, int padding)
    {
        var minX = a.X > b.X ? b.X : a.X;
        var minY = a.Y > b.Y ? b.Y : a.Y;
        var maxX = a.X > b.X ? a.X : b.X;
        var maxY = a.Y > b.Y ? a.Y : b.Y;

        return new Area(minX - padding, minY - padding, maxX + padding, maxY + padding);
    }

    public static Area CreateAroundPoint(Position position, int distance)
    {
        return new Area(position.X - distance, position.Y - distance, position.X + distance, position.Y + distance);
    }


    public static Area CreateAroundPoint(Position position, int width, int height)
    {
        return new Area(position.X - width, position.Y - height, position.X + width, position.Y + height);
    }


    public static Area CreateAroundPoint(int x, int y, int width, int height)
    {
        return new Area(x - width, y - height, x + width, y + height);
    }

    public override string ToString()
    {
        return $"{MinX.ToString()}/{MinY.ToString()}/{MaxX.ToString()}/{MaxY.ToString()}";
    }

    public static bool operator ==(Area left, Area right)
    {
        return left.MinX == right.MinX && left.MaxX == right.MaxX && left.MinY == right.MinY && left.MaxY == right.MaxY;
    }

    public static bool operator !=(Area left, Area right)
    {
        return !(left == right);
    }

    public bool Equals(Area other)
    {
        return MinX == other.MinX && MinY == other.MinY && MaxX == other.MaxX && MaxY == other.MaxY;
    }

    public override bool Equals(object? obj)
    {
        return obj is Area other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = MinX;
            hashCode = (hashCode * 397) ^ MinY;
            hashCode = (hashCode * 397) ^ MaxX;
            hashCode = (hashCode * 397) ^ MaxY;
            return hashCode;
        }
    }
}