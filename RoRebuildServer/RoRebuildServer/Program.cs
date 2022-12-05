using RebuildSharedData.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Server;
using Serilog;
using System.Diagnostics;
using RoRebuildServer.Data;
using RoRebuildServer.ScriptSystem;

if (args.Length > 0 && args[0] == "compile")
{
    ScriptLoader.CompilerEntryPoint();
    return;
}

CreateHostBuilder(args).Build().Run();

IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseSerilog(ServerLogger.GetLogger())
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<WebSocketGameServer>();
        });

