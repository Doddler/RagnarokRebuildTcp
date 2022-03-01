using RebuildSharedData.Extensions;

namespace RebuildSharedData.Data;

public class GameRandom
{
    private static readonly Random _global = new Random();
    private static int _seed = 0;
    private static Random? local;

    public static int Next() => Next(0, Int32.MaxValue);
    public static int Next(int max) => Next(0, max);


    public static short NextShort() => (short)Next(0, short.MaxValue);
    public static short NextShort(short max) => (short)Next(0, max);
    public static short NextShort(short min, short max) => (short)Next(min, max);

    public static float NextFloat() => (float)NextDouble();
    public static float NextFloat(float max) => (float)NextDouble(0, (double)max);
    public static float NextFloat(float min, float max) => (float)NextDouble((double)min, (double)max);

    public static double NextDouble(double max) => NextDouble(0f, max);

    private static void Initialize()
    {
        //lock (_global)
        //{
        //	if (_local == null)
        //	{
        //		if (_seed == 0)
        _seed = _global.Next();
        //		else
        //			Interlocked.Increment(ref _seed);

        local = new Random(_seed);
        //	}
        //}
    }

    public static int Next(int min, int max)
    {
        if (local == null)
            Initialize();

        return local!.Next(min, max + 1);
    }

    public static double NextDouble()
    {
        if (local == null)
            Initialize();

        return local!.NextDouble();
    }

    public static double NextDouble(double min, double max)
    {
        if (local == null)
            Initialize();

        return local!.NextDouble().Remap(0, 1, min, max);
    }
}