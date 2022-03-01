using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RebuildData.Server.Logging;
using RebuildData.Server.Pathfinding;
using RebuildZoneServer.Data.Management;
using RebuildZoneServer.EntityComponents;
using RebuildZoneServer.Networking;
using RebuildZoneServer.Sim;
using RebuildZoneServer.Util;

namespace RebuildZoneServer
{
    class ZoneWorker : BackgroundService
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
            DataManager.Initialize();
            
            if (DataManager.TryGetConfigInt("MaxSpawnTime", out var spawnTime))
            {
                Monster.MaxSpawnTimeInSeconds = spawnTime / 1000f;
                ServerLogger.Log($"Max monster spawn time set to {Monster.MaxSpawnTimeInSeconds} seconds.");
            }

            world = new World();
            NetworkManager.Init(world);

            Profiler.Init(0.005f); //logs events for frames that take longer than 5ms

            Time.Start();
            
            GC.Collect();
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //ServerLogger.RegisterLogger(logger);
            Initialize();
			
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var total = 0d;
            var max = 0d;
            var spos = 0;
            
            var totalNetwork = 0d;
            var totalEcs = 0d;
            var totalWorld = 0d;
            var maxNetwork = 0d;
            var maxEcs = 0d;
            var maxWorld = 0d;
            
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

                    var networkTime = Time.GetExactTime();

                    world.RunEcs();

                    var ecsTime = Time.GetExactTime();

                    world.Update();

                    var worldTime = Time.GetExactTime();

                    if (NetworkManager.IsSingleThreadMode)
                    {
                        await NetworkManager.ProcessOutgoingMessages();
                        networkTime += Time.GetExactTime() - worldTime;
                    }

                    var elapsed = Time.GetExactTime() - startTime;
                    //Console.WriteLine(elapsed);
                    total += elapsed;

                    Profiler.FinishFrame((float) elapsed);

                    var nt = networkTime - startTime;
                    var et = ecsTime - networkTime;
                    var wt = worldTime - ecsTime;

                    totalNetwork += nt;
                    totalEcs += et;
                    totalWorld += wt;

                    if (max < elapsed)
                        max = elapsed;

                    if (maxNetwork < nt)
                        maxNetwork = nt;
                    if (maxEcs < et)
                        maxEcs = et;
                    if (maxWorld < wt)
                        maxWorld = wt;

                    spos++;


                    var ms = (int) (elapsed * 1000) + 1;

                    if (ms < 10)
                        await Task.Delay(10 - ms, stoppingToken);

                    if (lastLog + noticeTime < Time.ElapsedTime)
                    {
                        var avg = (total / spos);
                        //var fps = 1 / avg;
                        var players = NetworkManager.PlayerCount;

                        avg *= 1000d;

                        //var avgNetwork = (totalNetwork / spos) * 1000d;
                        //var avgEcs = (totalEcs / spos) * 1000d;
                        //var avgWorld = (totalWorld / spos) * 1000d;

//#if DEBUG
//					var server = NetworkManager.State.Server;
//					ServerLogger.Log(
//						$"[Program] {players} players. Avg {avg:F2}ms / Peak {max * 1000:F2}ms "
//						+ $"(Net/ECS/World: {maxNetwork * 1000:F2}/{maxEcs * 1000:F2}/{maxWorld:F2}) "
//						+ $"Sent {server.Statistics.SentBytes}bytes/{server.Statistics.SentMessages}msg/{server.Statistics.SentPackets}packets");
//#else
                        ServerLogger.Log(
                            $"[Program] {players} players. Avg {avg:F2}ms / Peak {max * 1000:F2}ms "
                            + $"(Net/ECS/World: {maxNetwork * 1000:F2}/{maxEcs * 1000:F2}/{maxWorld:F2})");
//#endif
                        lastLog = Time.ElapsedTime;

                        total = 0;
                        max = 0;
                        spos = 0;

                        totalNetwork = 0;
                        totalWorld = 0;
                        totalEcs = 0;
                        maxNetwork = 0;
                        maxWorld = 0;
                        maxEcs = 0;

                        await NetworkManager.ScanAndDisconnect();
                    }
                }
            }
            catch (Exception e)
            {
                ServerLogger.LogError("Server threw exception!" + Environment.NewLine + e.Message);
            }

            logger.LogCritical("Oh no! We've dropped out of the processing loop! We will now shutdown.");
			appLifetime.StopApplication();
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Server shutting down at: {time}", DateTimeOffset.Now);

			NetworkManager.Shutdown();

            return base.StopAsync(cancellationToken);
        }
	}
}
