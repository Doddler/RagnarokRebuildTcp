using System.Diagnostics;
using System.Runtime;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Scripting;
using RoRebuildServer.Database;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Server;

internal class ZoneWorker : BackgroundService
{
    private const double TargetTickMs = 10;
    private const double GcSpareMs = 5; //headroom a tick has to finish with before we offer the slack to the GC
    private const double GcIntervalSeconds = 10; //floor between requests, so an idle server doesn't ask every tick

#if DEBUG
    private const double StatsIntervalSeconds = 15;
#else
    private const double StatsIntervalSeconds = 60;
#endif

    private readonly ILogger<ZoneWorker> logger;
    private readonly IServiceProvider services;
    private readonly IHostApplicationLifetime appLifetime;
    private World? world;

#if DEBUG
    public static bool IsMainThread;
#endif
    private bool isSafeExit = true;
    private Exception failureReason;

    public ZoneWorker(ILogger<ZoneWorker> logger, IServiceProvider services, IHostApplicationLifetime appLifetime)
    {
        this.logger = logger;
        this.services = services;
        this.appLifetime = appLifetime;
    }

    private async Task Initialize()
    {
        ServerLogger.Log("Ragnarok Rebuild Zone Server, starting up!");

        DistanceCache.Init();
        RoDatabase.Initialize();
        DataManager.Initialize();

        await ScriptGlobalManager.LoadGlobalsFromDatabase();

        var spawnTime = ServerConfig.DebugConfig.MaxSpawnTime;

        if (spawnTime > 0)
        {
            //Monster.MaxSpawnTimeInSeconds = spawnTime / 1000f;
            ServerLogger.Log($"Max monster spawn time set to {spawnTime / 1000f} seconds.");
        }

        world = new World();
        NetworkManager.Init(world);

        Time.Start();

        DataManager.ServerConfigScriptManager.PostServerStartEvent();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Initialize();

        var worldCancellation = World.Instance.GetCancellationToken;

        Debug.Assert(world != null);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var stats = new Stats(StatsIntervalSeconds, TargetTickMs);
        var nextCollect = Time.ElapsedTime + GcIntervalSeconds;

        try
        {
            while (!stoppingToken.IsCancellationRequested && !worldCancellation.IsCancellationRequested)
            {
                Time.Update();

                var startTime = Time.GetExactTime();

                if (NetworkManager.IsSingleThreadMode)
                    await NetworkManager.ProcessOutgoingMessages();

                await NetworkManager.ProcessMainThreadMessages();

                world.Update();

                await NetworkManager.ScanAndDisconnect();

                var elapsedMs = (Time.GetExactTime() - startTime) * 1000d;
                var spareMs = TargetTickMs - elapsedMs;

                //it's been a while and we had a fast frame, so may as well? No idea if this is a bad idea or not.
                if (spareMs > GcSpareMs && Time.ElapsedTime > nextCollect)
                {
                    GC.Collect(2, GCCollectionMode.Optimized, false);
                    nextCollect = Time.ElapsedTime + GcIntervalSeconds;
                }

                //Sleep out whatever is left of the tick, rounded so a sub-millisecond frame doesn't give up a whole one.
                var remainingMs = (int)Math.Round(spareMs);
                if (remainingMs > 0)
                    await Task.Delay(remainingMs, stoppingToken);
                else
                    await Task.Yield();

                stats.Record(elapsedMs);
                stats.ReportIfDue(NetworkManager.PlayerCount);
            }
        }
        catch (Exception e)
        {
            if (e is TaskCanceledException)
                return;

            ServerLogger.LogError("Server threw exception!" + Environment.NewLine + e);
            failureReason = e;
            isSafeExit = false;
        }

        if (!NetworkManager.IsServerOpen)
        {
            CommandBuilder.AddAllPlayersAsRecipients();
            if (isSafeExit)
                CommandBuilder.SendServerMessage("The server is now shutting down, you will be disconnected shortly.");
            else
                CommandBuilder.SendServerMessage("The server is shutting down due to an internal error.");
            CommandBuilder.ClearRecipients();
        }

        NetworkManager.TriggerAllCancellations();

        if (worldCancellation.IsCancellationRequested)
            logger.LogCritical("The server has left the main processing loop due to an admin initiated shutdown.");
        else
            logger.LogCritical("Oh no! We've dropped out of the processing loop! We will now shutdown.");
        appLifetime.StopApplication();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Server shutting down at: {time}", DateTimeOffset.Now);

        await NetworkManager.ScanAndDisconnect();

        NetworkManager.Shutdown();
        //network manager shutdown should queue all players to save, so now we wait for save to finish
        await RoDatabase.Shutdown();

        await base.StopAsync(cancellationToken);

        //makes it real clear in the logs where the server shuts down
        logger.LogInformation("Server is now shut down!");
        logger.LogInformation("=======================================================================================");
        logger.LogInformation("=======================================================================================");

        if (!isSafeExit)
            throw failureReason; //this will cause the application to terminate in failure, which should trigger the service to be restarted.
    }
    private class Stats
    {
        private const double FirstReportSeconds = 5; //check in shortly after startup regardless of the interval

        private readonly double reportInterval;
        private readonly double budgetMs;

        private double windowStart;
        private double nextReport;
        private TimeSpan lastGcPause;
        private int lastGen0;
        private int lastGen1;
        private int lastGen2;

        private double totalMs;
        private double peakMs;
        private int frames;
        private int overBudget;

        public Stats(double reportIntervalSeconds, double budgetMs)
        {
            reportInterval = reportIntervalSeconds;
            this.budgetMs = budgetMs;

            windowStart = Time.ElapsedTime;
            nextReport = Time.ElapsedTime + FirstReportSeconds;

            SampleCollections();
        }

        private void SampleCollections()
        {
            lastGcPause = GC.GetTotalPauseDuration();
            lastGen0 = GC.CollectionCount(0);
            lastGen1 = GC.CollectionCount(1);
            lastGen2 = GC.CollectionCount(2);
        }

        public void Record(double elapsedMs)
        {
            totalMs += elapsedMs;
            frames++;

            if (elapsedMs > peakMs)
                peakMs = elapsedMs;

            if (elapsedMs > budgetMs)
                overBudget++;
        }

        public void ReportIfDue(int players)
        {
            if (Time.ElapsedTime < nextReport || frames == 0)
                return;

            var window = Time.ElapsedTime - windowStart;

            var gen0 = GC.CollectionCount(0) - lastGen0;
            var gen1 = GC.CollectionCount(1) - lastGen1;
            var gen2 = GC.CollectionCount(2) - lastGen2;
            var heapMb = GC.GetTotalMemory(false) / (1024d * 1024d);

            //Most windows collect nothing, so the generation breakdown only earns its space when one of them ran.
            var gc = gen0 + gen1 + gen2 == 0
                ? ""
                : $" | GC {(GC.GetTotalPauseDuration() - lastGcPause).TotalMilliseconds:F1}ms (g0 {gen0} g1 {gen1} g2 {gen2})";

            //Avg and Peak cover the work only, Tick is the whole loop period including the sleep. So Tick is the
            //rate the simulation steps at, and Tick minus Avg is time spent waiting.
            ServerLogger.Log($"[ZoneWorker] {players} players | {window:F0}s | Avg {totalMs / frames:F2}ms" +
                             $" | Peak {peakMs:F2}ms | Tick {window / frames * 1000d:F2}ms | Over {overBudget}" +
                             $" | Heap {heapMb:F0}MB{gc}");

            windowStart = Time.ElapsedTime;
            nextReport = Time.ElapsedTime + reportInterval;

            SampleCollections();

            totalMs = 0;
            peakMs = 0;
            frames = 0;
            overBudget = 0;
        }
    }
}