using System.Numerics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;

namespace RoRebuildServer.Simulation.Util;

public static class MathHelper
{
    private static readonly float[] ResistTable;
    private static readonly float[] BoostTable;
    private static readonly float[] DefTable;

    public const float Deg2Rad = 0.017453292f;
    public const float Rad2Deg = 57.29578f;

    static MathHelper()
    {
        ResistTable = new float[1000];
        BoostTable = new float[1000];
        DefTable = new float[1500];

        for (var i = 0; i < 1000; i++)
        {
            ResistTable[i] = MathF.Pow(0.99f, i);
            BoostTable[i] = MathF.Pow(1.01f, i);
        }

        for (var i = 0; i < 1500; i++)
        {
            //the def table stores 1 decimal place, so 300 = 30.0 def
            //up to 30 def we use straight 1 def = 1%, above that diminishing returns
            //10 def -> 10% reduction       70 def -> 63.1% reduction
            //20 def -> 20% reduction       80 def -> 69.5% reduction
            //30 def -> 30% reduction       90 def -> 75.3% reduction
            //40 def -> 39.6% reduction     100 def -> 80.5% reduction
            //50 def -> 48.2% reduction     110 def -> 85.2% reduction
            //60 def -> 56.0% reduction     120 def -> 89.5% reduction

            if (i < 300)
                DefTable[i] = 1 - i / 1000f;
            else
                DefTable[i] = MathF.Max(0.1f, MathF.Pow(0.99f, i / 10f - 30) - 0.3f);
        }
    }

    public static float AngleFromDirection(Direction dir, Position offset)
    {
        var angle = Directions.GetAngleForDirection(dir);
        var angle2 = (MathF.Atan2(offset.X, offset.Y) * MathHelper.Rad2Deg) % 360;

        return 180 - MathF.Abs(MathF.Abs(angle - angle2) - 180);
    }

    /// <summary>
    /// Checks if a point is within a rectangle representing a path starting from lineStart and pointing towards lineTarget.
    /// </summary>
    /// <param name="lineStart">The starting point of the line.</param>
    /// <param name="lineTarget">The ending target of the line.</param>
    /// <param name="checkPoint">The point you want to see if falls within the line.</param>
    /// <param name="length">The length of the line. The line may stop before the target or extend past the target based on what the length is set to.</param>
    /// <param name="width">How wide the line is.</param>
    /// <param name="backtrackDistance">How far behind the start point you want to extend the line. Without this a checkPoint matching the lineStart may not match due to floating point accuracy.</param>
    /// <returns>Returns true if the checkPoint is within the line bounds.</returns>
    public static bool IsPointInLinePath(Position lineStart, Position lineTarget, Position checkPoint, float length, float width, float backtrackDistance = 0.1f)
    {
        var t1 = checkPoint.ToVector2();
        var p1 = lineStart.ToVector2();
        var p2 = lineTarget.ToVector2();

        var dirNorm = Vector2.Normalize(p2 - p1);
        p2 = p1 + dirNorm * length;
        p1 -= dirNorm * backtrackDistance;

        var t = Vector2.Dot(t1 - p1, dirNorm);
        var clamped = float.Clamp(t, 0, (p2 - p1).Length());
        var closest = p1 + dirNorm * clamped;
        var dist = Vector2.Distance(closest, t1);

        if (dist > width)
            return false;

        //we're within the width of the line... but are we between the two points?
        var d1 = Vector2.Distance(closest, p1);
        var d2 = Vector2.Distance(closest, p2);

        return d1 < length && d2 < length;
    }

    /// <summary>
    /// Checks if a point is within a rectangle representing a path starting from lineStart and pointing towards lineTarget.
    /// </summary>
    /// <param name="lineStart">The starting point of the line.</param>
    /// <param name="lineTarget">The ending target of the line.</param>
    /// <param name="checkPoint">The point you want to see if falls within the line.</param>
    /// <param name="length">The length of the line. The line may stop before the target or extend past the target based on what the length is set to.</param>
    /// <param name="width">How wide the line is.</param>
    /// <param name="backtrackDistance">How far behind the start point you want to extend the line. Without this a checkPoint matching the lineStart may not match due to floating point accuracy.</param>
    /// <returns>Returns true if the checkPoint is within the line path and how far away that point is to the closest point to it along the line.</returns>
    public static (bool, float) IsPointInLinePathWithProjectedDistance(Position lineStart, Position lineTarget, Position checkPoint, float length, float width, float backtrackDistance = 0.1f)
    {
        var t1 = checkPoint.ToVector2();
        var p1 = lineStart.ToVector2();
        var p2 = lineTarget.ToVector2();

        var dirNorm = Vector2.Normalize(p2 - p1);
        p2 = p1 + dirNorm * length;
        p1 -= dirNorm * backtrackDistance;

        var t = Vector2.Dot(t1 - p1, dirNorm);
        var clamped = float.Clamp(t, 0, (p2 - p1).Length());
        var closest = p1 + dirNorm * clamped;
        var dist = Vector2.Distance(closest, t1);

        if (dist > width)
            return (false, -1);

        //we're within the width of the line... but are we between the two points?
        var d1 = Vector2.Distance(closest, p1);
        var d2 = Vector2.Distance(closest, p2);

        var realLength = length + backtrackDistance;

        return (d1 < realLength && d2 < realLength, Vector2.Distance(p1, closest));
    }

    public static float PowScaleDown(int value)
    {
        if (value >= 0 && value < ResistTable.Length)
            return ResistTable[value];
        return MathF.Pow(0.99f, value);
    }

    public static float PowScaleUp(int value)
    {
        if (value >= 0 && value < BoostTable.Length)
            return BoostTable[value];
        return MathF.Pow(1.01f, value);
    }

    public static float DefValueLookup(int value, int refineDef = 0)
    {
        var intVal = value * 10 + refineDef * 7;
        if (intVal >= 0 && intVal < DefTable.Length)
            return DefTable[intVal];

        return MathF.Max(0.1f, MathF.Pow(0.99f, intVal / 10f - 30) - 0.3f);
    }

    public static int Clamp(this int val, int min, int max)
    {
        if (val < min)
            val = min;
        else if (val > max)
            val = max;
        return val;
    }


    public static float Clamp(this float val, float min, float max)
    {
        if (val < min)
            val = min;
        else if (val > max)
            val = max;
        return val;
    }

    public static float Clamp01(this float val)
    {
        if (val < 0f)
            val = 0f;
        else if (val > 1f)
            val = 1f;
        return val;
    }

    public static float Lerp(float firstFloat, float secondFloat, float by)
    {
        return firstFloat * (1 - by) + secondFloat * by;
    }
}