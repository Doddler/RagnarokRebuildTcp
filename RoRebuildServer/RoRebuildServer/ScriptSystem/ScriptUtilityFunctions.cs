using RebuildSharedData.Data;

namespace RoRebuildServer.ScriptSystem;

public static class ScriptUtilityFunctions
{
    public static float Sin(float angle)
    {
        return (float)Math.Sin(angle);
    }

    public static float Cos(float angle)
    {
        return (float)Math.Cos(angle);
    }

    public static Position Position(float x, float y)
    {
        return new Position((int)MathF.Round(x), (int)MathF.Round(y));
    }

    public static int GetX(Position p) => p.X;
    public static int GetY(Position p) => p.Y;
}