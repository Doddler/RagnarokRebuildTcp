using RebuildSharedData.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Server;
using Serilog;

CreateHostBuilder(args).Build().Run();

IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseSerilog(ServerLogger.GetLogger())
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<WebSocketGameServer>();
        });

