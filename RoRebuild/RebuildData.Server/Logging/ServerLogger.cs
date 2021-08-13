using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace RebuildData.Server.Logging
{
	public static class ServerLogger
    {
        private static Logger logger = new LoggerConfiguration().WriteTo.Console(theme: SystemConsoleTheme.Colored).CreateLogger();

        public static Logger GetLogger() => logger;

        //private static readonly object[] param = new object[0]; //to avoid allocating a new array each time when logging

        //public static void RegisterLogger(ILogger log) => new LoggerConfiguration().WriteTo.Console().CreateLogger();

        [Conditional("DEBUG")]
        public static void Debug(string message) => logger.Debug(message);
        public static void Log(string message) => logger.Information(message);
        public static void LogWarning(string error) => logger.Warning(error);
        public static void LogError(string error) => logger.Error(error);
    }
}
