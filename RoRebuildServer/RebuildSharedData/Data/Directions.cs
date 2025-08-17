using RebuildSharedData.Enum;

namespace RebuildSharedData.Data;

public static class Directions
{
    public static (int x, int y) GetXYForDirection(Direction dir)
    {
        return dir switch
        {
            Direction.East => (1, 0),
            Direction.SouthEast => (1, -1),
            Direction.South => (0, -1),
            Direction.SouthWest => (-1, -1),
            Direction.West => (-1, 0),
            Direction.NorthWest => (-1, 1),
            Direction.North => (0, 1),
            Direction.NorthEast => (1, 1),
            _ => (1, 0)
        };
    }

    public static Direction GetDirectionFromCoordinates(int x, int y)
    {
        if (x < 0 && y < 0) return Direction.SouthWest;
        if (x < 0 && y == 0) return Direction.West;
        if (x < 0 && y > 0) return Direction.NorthWest;
        if (x == 0 && y < 0) return Direction.South;
        //if (x == 0 && y == 0) return Direction.None;
        if (x == 0 && y > 0) return Direction.North;
        if (x > 0 && y < 0) return Direction.SouthEast;
        if (x > 0 && y == 0) return Direction.East;
        if (x > 0 && y > 0) return Direction.NorthEast;
        return Direction.None;
    }
    
    public static Direction GetFacingForAngle(float angle)
    {
        if (angle > 157.5f) return Direction.South;
        if (angle > 112.5f) return Direction.SouthWest;
        if (angle > 67.5f) return Direction.West;
        if (angle > 22.5f) return Direction.NorthWest;
        if (angle > -22.5f) return Direction.North;
        if (angle > -67.5f) return Direction.NorthEast;
        if (angle > -112.5f) return Direction.East;
        if (angle > -157.5f) return Direction.SouthEast;
        return Direction.South;
    }
    
    public static Direction Get4DirFacingForAngle(float angle)
    {
        if (angle > 90f) return Direction.SouthWest;
        if (angle > 0f) return Direction.NorthWest;
        if (angle > -90) return Direction.NorthEast;
        return Direction.SouthEast;
    }


    public static float GetAngleForDirection(Direction d)
    {
        switch (d)
        {
            case Direction.North: return 0f;
            case Direction.NorthEast: return 45f;
            case Direction.East: return 90f;
            case Direction.SouthEast: return 135f;
            case Direction.South: return 180f;
            case Direction.SouthWest: return 225f;
            case Direction.West: return 270f;
            case Direction.NorthWest: return 315f;
            
        }

        return 0f;
    }

    public static Direction GetIntercardinalDirection(this Direction d)
    {
        switch (d)
        {
            case Direction.West:
            case Direction.NorthWest: return Direction.NorthWest;
            case Direction.North:
            case Direction.NorthEast: return Direction.NorthEast;
            case Direction.East:
            case Direction.SouthEast: return Direction.SouthEast;
            case Direction.South:
            case Direction.SouthWest: return Direction.SouthWest;
            case Direction.None: return Direction.None;
        }

        throw new Exception("Invalid Direction: " + ((int)d).ToString());
    }

    public static Direction GetReverseDirection(Direction d)
    {
        switch (d)
        {
            case Direction.NorthWest: return Direction.SouthEast;
            case Direction.North: return Direction.South;
            case Direction.NorthEast: return Direction.SouthWest;
            case Direction.West: return Direction.East;
            case Direction.East: return Direction.West;
            case Direction.SouthWest: return Direction.NorthEast;
            case Direction.South: return Direction.North;
            case Direction.SouthEast: return Direction.NorthWest;
            case Direction.None: return Direction.None;
        }

        throw new Exception("Invalid Direction: " + ((int)d).ToString());
    }
}