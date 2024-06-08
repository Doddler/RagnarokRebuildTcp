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

    public static Direction Flip(this Direction dir)
    {
        switch (dir)
        {
            case Direction.South: return Direction.North;
            case Direction.SouthWest: return Direction.NorthEast;
            case Direction.West: return Direction.East;
            case Direction.NorthWest: return Direction.SouthEast;
            case Direction.North: return Direction.South;
            case Direction.NorthEast: return Direction.SouthWest;
            case Direction.East: return Direction.West;
            case Direction.SouthEast: return Direction.NorthWest;
            default:
                return dir;
        }
    }

    //for debugging mostly
    public static int NumPadDirection(this Direction dir)
    {
        switch (dir)
        {
            case Direction.South: return 2;
            case Direction.SouthWest: return 1;
            case Direction.West: return 4;
            case Direction.NorthWest: return 7;
            case Direction.North: return 8;
            case Direction.NorthEast: return 9;
            case Direction.East: return 6;
            case Direction.SouthEast: return 3;
            default:
                return 0;
        }
    }
}