using System.Text;
using RebuildSharedData.Data;
using RebuildSharedData.Extensions;
using RebuildSharedData.Util;

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

    public static float DegToRad(float angle)
    {
        return MathF.PI / 180f * angle;
    }

    public static Position Position(float x, float y)
    {
        return new Position((int)MathF.Round(x), (int)MathF.Round(y));
    }

    public static string CleanCsString(string str)
    {
        var sb = new StringBuilder(str.Length);
        for (var i = 0; i < str.Length; i++)
        {
            var s = str[i];
            if (s < '0' || s > 'z')
                continue;
            if (s > '9' && s < 'A')
                continue;
            if (s > 'Z' && s < 'a')
                continue;
            sb.Append(s);
        }
        return sb.ToString();
    }

    public static float Remap(float value, float from1, float to1, float from2, float to2) => value.Remap(from1, to1, from2, to2);
    public static int Remap(int value, int from1, int to1, int from2, int to2) => (int)Remap((float)value, (float)from1, (float)to1, (float)from2, (float)to2);

    public static int GetX(Position p) => p.X;
    public static int GetY(Position p) => p.Y;

    public static int Random(int max) => GameRandom.Next(max);
    public static int Random(int min, int max) => GameRandom.Next(min, max);
    public static int RandomInclusive(int max) => GameRandom.NextInclusive(max);
    public static int RandomInclusive(int min, int max) => GameRandom.NextInclusive(min, max);

    public static int DeterministicRandom(int seed, int max)
    {
        var calcBase = (DateTime.Today.ToFileTimeUtc() / 86400) * 100 + seed;

        var step1 = (calcBase << 11) ^ calcBase;
        var step2 = (step1.RotateRight(8)) ^ step1;
        step2 = Math.Abs(step2);

        return (int)(step2 % max);
    }
}