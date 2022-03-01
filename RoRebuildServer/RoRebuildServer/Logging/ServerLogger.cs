using System.Diagnostics;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;

namespace RoRebuildServer.Logging;

public static class ServerLogger
{
#if DEBUG
    private static Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Console(theme: SystemConsoleTheme.Colored).CreateLogger();
#else
    private static Logger logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Console(theme: SystemConsoleTheme.Colored).CreateLogger();
#endif

    public static Logger GetLogger() => logger;

    //private static readonly object[] param = new object[0]; //to avoid allocating a new array each time when logging

    //public static void RegisterLogger(ILogger log) => new LoggerConfiguration().WriteTo.Console().CreateLogger();

    [Conditional("DEBUG")]
    public static void Debug(string message) => logger.Debug(message);
    public static void Log(string message) => logger.Information(message);
    public static void LogWarning(string error) => logger.Warning(error);
    public static void LogError(string error) => logger.Error(error);
}