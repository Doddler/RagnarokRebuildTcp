using System.Diagnostics;
using RoRebuildServer.Data;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;
using ILogger = Serilog.ILogger;

namespace RoRebuildServer.Logging;

public static class ServerLogger
{
    private static ILogger logger = new LoggerConfiguration().Enrich.FromLogContext().ReadFrom.Configuration(ServerConfig.Configuration).CreateLogger();

    public static ILogger GetLogger() => logger;
    public static void SetLogger(ILogger newLogger) => logger = newLogger;

    [Conditional("DEBUG")]
    public static void Debug(string message) => logger.Debug(message);
    public static void Log(string message) => logger.Information(message);
    public static void LogWarning(string error) => logger.Warning(error);
    public static void LogWarningWithStackTrace(string error) => logger.Warning(error + Environment.NewLine + Environment.StackTrace);
    public static void LogError(string error) => logger.Error(error);
    public static void LogErrorWithStackTrace(string error) => logger.Warning(error + Environment.NewLine + Environment.StackTrace);
    public static void LogVerbose(string error) => logger.Verbose(error);
}