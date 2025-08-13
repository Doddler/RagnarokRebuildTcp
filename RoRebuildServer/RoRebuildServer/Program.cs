using RoRebuildServer.Logging;
using RoRebuildServer.Server;
using Serilog;
using RoRebuildServer.ScriptSystem;


if (args.Length > 0 && args[0] == "compile")
{
    ScriptLoader.CompilerEntryPoint();
    return;
}

try
{
    if (!Console.IsOutputRedirected)
    {
        Console.Clear();
    }
}
catch (IOException)
{
    Console.WriteLine("[WARN] Console.Clear() failed — skipping.");
}

CreateHostBuilder(args).Build().Run();

IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseSerilog(ServerLogger.GetLogger())
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<WebSocketGameServer>();
        });

