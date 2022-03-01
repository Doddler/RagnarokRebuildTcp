using System.Diagnostics;

namespace RoRebuildServer.Simulation.Util;

public static class Time
{
    public static double DeltaTime;
    public static double ElapsedTime;
    public static float ElapsedTimeFloat;
    public static float DeltaTimeFloat;
    public static ulong UpdateCount;

    private static Stopwatch stopWatch;

    private static double[] previousFrameTimes;
    private static int frameIndex;
    private static int frameCount;

    private const int SampleCount = 100;

    

    public static void Start()
    {
        stopWatch = new Stopwatch();
        stopWatch.Start();
        previousFrameTimes = new double[SampleCount];
    }

    public static void Update()
    {
        if (stopWatch == null)
            throw new Exception("Attempting to update Time without it being initialized");
        var newTime = stopWatch.Elapsed.TotalSeconds;
        DeltaTime = (newTime - ElapsedTime);
        DeltaTimeFloat = (float)DeltaTime;
        ElapsedTime = newTime;
        ElapsedTimeFloat = (float)newTime;

        previousFrameTimes[frameIndex] = DeltaTime;
        frameIndex++;
        if (frameCount < frameIndex)
            frameCount++;
        if (frameIndex > SampleCount - 1)
            frameIndex = 0;

        if (UpdateCount == ulong.MaxValue)
            UpdateCount = 0;

        UpdateCount++;
        
    }

    public static double GetExactTime()
    {
        return stopWatch.Elapsed.TotalSeconds;
    }

    public static int MinutesSinceStartup()
    {
        return (int)MathF.Round(ElapsedTimeFloat / 60f);
    }

    public static int MsSinceLastUpdate()
    {
        var time = stopWatch.Elapsed.TotalSeconds - ElapsedTime;
        return (int)(time * 1000);
    }

    public static double GetAverageFrameTime()
    {
        double total = 0;
        for (var i = 0; i < frameCount; i++)
            total += previousFrameTimes[i];
        return total / frameCount;
    }

    public static double GetMaxFrameTime() => previousFrameTimes.Max();

    public static void ManuallyIncrement(double deltaTime)
    {
        ElapsedTime += deltaTime;
        DeltaTime = deltaTime;
        DeltaTimeFloat = (float)DeltaTime;
    }
}