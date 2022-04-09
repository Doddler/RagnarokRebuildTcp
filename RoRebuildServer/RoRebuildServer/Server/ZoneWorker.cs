using System.Diagnostics;
using System.Runtime;
using RoRebuildServer.Data;
using RoRebuildServer.Database;
using RoRebuildServer.EntityComponents;
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
    private World world;

    public ZoneWorker(ILogger<ZoneWorker> logger, IServiceProvider services, IHostApplicationLifetime appLifetime)
    {
        this.logger = logger;
        this.services = services;
        this.appLifetime = appLifetime;
    }

    private void Initialize()
    {
        ServerLogger.Log("Ragnarok Rebuild Zone Server, starting up!");

        DistanceCache.Init();
        RoDatabase.Initialize();
        DataManager.Initialize();

        if (DataManager.TryGetConfigInt("MaxSpawnTime", out var spawnTime))
        {
            Monster.MaxSpawnTimeInSeconds = spawnTime / 1000f;
            ServerLogger.Log($"Max monster spawn time set to {Monster.MaxSpawnTimeInSeconds} seconds.");
        }
        
        world = new World();
        NetworkManager.Init(world);
            
        Time.Start();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Initialize();
        
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var total = 0d;
        var max = 0d;
        var spos = 0;

#if DEBUG
        var noticeTime = 5f;
#else
			var noticeTime = 60f;
#endif
        var lastLog = Time.ElapsedTime - noticeTime + 5f; //make the first check-in 5s after start no matter what

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Time.Update();
                
                var startTime = Time.GetExactTime();

                await NetworkManager.ProcessIncomingMessages();

                if (NetworkManager.IsSingleThreadMode)
                    await NetworkManager.ProcessOutgoingMessages();
                
                world.Update();

                await NetworkManager.ScanAndDisconnect();

                //if we spent less than 10ms on this frame, sleep for the remaining time
                var elapsed = Time.GetExactTime() - startTime;
                var ms = (int)(elapsed * 1000) + 1;
                if (ms < 10)
                    await Task.Delay(10 - ms, stoppingToken);
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

                    ServerLogger.Log($"[Program] {players} players. Avg {avg:F2}ms / Peak {max * 1000:F2}ms");
                    
                    total = 0;
                    spos = 0;
                    max = 0;
                    lastLog = Time.ElapsedTime;
                }
            }
        }
        catch (Exception e)
        {
            if (e is TaskCanceledException)
                return;

            ServerLogger.LogError("Server threw exception!" + Environment.NewLine + e);
        }

        logger.LogCritical("Oh no! We've dropped out of the processing loop! We will now shutdown.");
        appLifetime.StopApplication();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Server shutting down at: {time}", DateTimeOffset.Now);

        NetworkManager.Shutdown();
        //network manager shutdown should queue all players to save, so now we wait for save to finish
        await RoDatabase.Shutdown();

        await base.StopAsync(cancellationToken);
    }
}