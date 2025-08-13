using System.Diagnostics;

namespace RoRebuildServer.Simulation.Util;

public static class Time
{
    public static double DeltaTime;
    public static double ElapsedTime;
    public static float ElapsedTimeFloat;
    public static float DeltaTimeFloat;
    public static ulong UpdateCount;
    
    public static byte RollingTimerGen;
    public static float RollingTimerTime;
    public static float RolloverTime;

    private static readonly Stopwatch StopWatch = new();
    private static readonly Stopwatch DiagnosticsTimer = new();
    private static readonly double[] PreviousFrameTimes = new double[SampleCount];
    
    private static int frameIndex;
    private static int frameCount;

    private const int SampleCount = 100;

    private static double lastMinorDiagnosticsTime;
    private static double lastMajorDiagnosticsTime;

    public static void Start()
    {
        StopWatch.Start();
    }

    public static void Update()
    {
        if (StopWatch == null || !StopWatch.IsRunning)
            throw new Exception("Attempting to update Time without it being initialized");
        var newTime = StopWatch.Elapsed.TotalSeconds;
        DeltaTime = (newTime - ElapsedTime);
        DeltaTimeFloat = (float)DeltaTime;
        ElapsedTime = newTime;
        ElapsedTimeFloat = (float)newTime;
        RollingTimerTime += DeltaTimeFloat;

        PreviousFrameTimes[frameIndex] = DeltaTime;
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
        return StopWatch.Elapsed.TotalSeconds;
    }

    public static int MinutesSinceStartup()
    {
        return (int)MathF.Round(ElapsedTimeFloat / 60f);
    }

    public static int MsSinceLastUpdate()
    {
        var time = StopWatch.Elapsed.TotalSeconds - ElapsedTime;
        return (int)(time * 1000);
    }

    public static double GetAverageFrameTime()
    {
        double total = 0;
        for (var i = 0; i < frameCount; i++)
            total += PreviousFrameTimes[i];
        return total / frameCount;
    }

    public static double GetMaxFrameTime() => PreviousFrameTimes.Max();

    public static void ManuallyIncrement(double deltaTime)
    {
        ElapsedTime += deltaTime;
        DeltaTime = deltaTime;
        DeltaTimeFloat = (float)DeltaTime;
    }

    public static void ResetDiagnosticsTimer()
    {
        DiagnosticsTimer.Restart();
        lastMinorDiagnosticsTime = 0;
        lastMajorDiagnosticsTime = 0;
    }

    public static double SampleDiagnosticsSubTime()
    {
        var t = DiagnosticsTimer.Elapsed.TotalSeconds;
        var elapsed = t - lastMinorDiagnosticsTime;
        lastMinorDiagnosticsTime = t;

        return elapsed;
    }

    public static double SampleDiagnosticsTime()
    {
        var t = DiagnosticsTimer.Elapsed.TotalSeconds;
        var elapsed = t - lastMajorDiagnosticsTime;
        lastMajorDiagnosticsTime = t;
        lastMinorDiagnosticsTime = t;

        return elapsed;
    }

    public static double CurrentDiagnosticsTime() => DiagnosticsTimer.Elapsed.TotalSeconds;


}