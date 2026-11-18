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

        var timeScale = ServerConfig.DebugConfig.TimeScale;
        if (timeScale <= 0)
            throw new Exception($"ServerDebugConfig.TimeScale must be greater than 0 (was {timeScale}).");
        if (ServerConfig.OperationConfig.ServerTickMs <= 0)
            throw new Exception($"ServerOperationConfig.ServerTickMs must be greater than 0 (was {ServerConfig.OperationConfig.ServerTickMs}).");
        Time.TimeScale = timeScale;
        if (timeScale != 1)
            ServerLogger.LogWarning($"Server time scale set to {timeScale}x.");

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

        var total = 0d;
        var max = 0d;
        var spos = 0;

#if DEBUG
        var noticeTime = 15f;
        var noticeMax = 60f;
#else
        var noticeTime = 30f;
		var noticeMax = 600f;
#endif
        var lastLog = Time.ElapsedTime - noticeTime + 5f; //make the first check-in 5s after start no matter what

        var loopCount = 0;
        
        var targetFrameMs = ServerConfig.OperationConfig.ServerTickMs / Time.TimeScale;
        var gcLoopInterval = (int)(10_000 / targetFrameMs);

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

                //if we finished this frame early, sleep for the remaining time
                var elapsed = Time.GetExactTime() - startTime;
                loopCount++;
                var remainingMs = targetFrameMs - elapsed * 1000;
                if (remainingMs > 0)
                {
                    if (loopCount > gcLoopInterval && elapsed < 5)
                    {
                        //it's been a while and we had a fast frame, so may as well? No idea if this is a bad idea or not.
                        GC.Collect(2, GCCollectionMode.Optimized, false);
                        loopCount = 0;
                    }
                    else if (remainingMs >= 1)
                        await Task.Delay((int)remainingMs, stoppingToken);
                    else
                        await Task.Yield(); // sub-ms frames (high time scales) just run back to back
                }
                else
                    await Task.Yield();

                total += elapsed;

                if (max < elapsed)
                    max = elapsed;

                spos++;

                if (lastLog + noticeTime < Time.ElapsedTime)
                {
                    var avg = (total / spos);
                    //var fps = 1 / avg;
                    var players = NetworkManager.PlayerCount;

                    avg *= 1000d;

#if DEBUG
                    if (max > 0.1f)
                        ServerLogger.Log($"[ZoneWorker] {players} players. Stats over last {noticeTime}s : Avg {avg:F2}ms / Peak {max * 1000:F2}ms (GC Time: {GC.GetTotalPauseDuration()})");
                    else
                        ServerLogger.Debug($"[ZoneWorker] {players} players. Stats over last {noticeTime}s : Avg {avg:F2}ms / Peak {max * 1000:F2}ms (GC Time: {GC.GetTotalPauseDuration()})");
#else
                    ServerLogger.Log($"[ZoneWorker] {players} players. Stats over last {noticeTime}s : Avg {avg:F2}ms / Peak {max * 1000:F2}ms");
#endif

                    total = 0;
                    spos = 0;
                    max = 0;
                    lastLog = Time.ElapsedTime;
                    noticeTime += 30;
                    if (noticeTime > noticeMax)
                        noticeTime = noticeMax;
                }
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
}