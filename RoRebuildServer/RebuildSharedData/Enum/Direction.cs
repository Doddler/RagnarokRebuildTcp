namespace RebuildSharedData.Enum;

public enum Direction : byte
{
    South,
    SouthWest,
    West,
    NorthWest,
    North,
    NorthEast,
    East,
    SouthEast,
    None
}

public static class DirectionExtensions
{
    public static bool IsDiagonal(this Direction dir)
    {
        if (dir == Direction.NorthEast || dir == Direction.NorthWest ||
            dir == Direction.SouthEast || dir == Direction.SouthWest)
            return true;
        return false;
    }
}