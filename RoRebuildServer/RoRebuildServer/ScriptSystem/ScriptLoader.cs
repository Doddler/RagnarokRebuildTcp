using RoRebuildServer.Data;
using RoRebuildServer.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Serilog.Events;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Formatting;
using ILogger = Serilog.ILogger;
using System.Globalization;

namespace RoRebuildServer.ScriptSystem;

public record CompilerLogEvent(LogEventLevel Level, string Message);

public class CompilerLogCollector : Serilog.ILogger
{
    public List<LogEvent> LogEvents { get; set; } = new();

    public void Write(LogEvent logEvent)
    {
        LogEvents.Add(logEvent);
        Console.WriteLine(JsonSerializer.Serialize(new CompilerLogEvent(logEvent.Level, logEvent.MessageTemplate.Render(logEvent.Properties))));
    }
}

public static class ScriptLoader
{
    public static Assembly LoadAssembly()
    {
        if (ServerConfig.DataConfig.CompileScriptsOutOfProcess)
        {
            StartOutOfProcessCompiler();
            return LoadExisting();
        }
        else
            return CompileScripts();
    }
    
    private static void StartOutOfProcessCompiler()
    {
        ServerLogger.Log("Starting out of process script compiler.");

        Process currentProcess = Process.GetCurrentProcess();

        var startInfo = new ProcessStartInfo(currentProcess.MainModule!.FileName)
        {
            Arguments = "compile",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var process = new Process() { StartInfo = startInfo };
        
        try
        {
            process.Start();
            
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    var str = JsonSerializer.Deserialize<CompilerLogEvent>(line);
                    if(str != null)
                        ServerLogger.GetLogger().Write(str.Level, str.Message);
                    else
                        ServerLogger.GetLogger().Write(LogEventLevel.Warning, $"Received message from out of process script compiler, but message was malformed. Message: {line}");
                }
            }

            process.WaitForExit();
            
            return;
        }
        catch (Exception)
        {
            ServerLogger.LogError($"Host process caused an exception while running the compiler process!");
            throw;
        }
    }

    public static void CompilerEntryPoint()
    {
        ServerLogger.SetLogger(new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Console(outputTemplate: "{Message}{NewLine}{Exception}").CreateLogger());
        var logger = new CompilerLogCollector();
        try
        {
            ServerLogger.SetLogger(logger);
            CompileScripts();
        }
        catch (Exception e)
        {
            ServerLogger.LogError(e.Message);
        }
    }
    
    public static Assembly LoadExisting()
    {
        var dll = Path.Combine(ServerConfig.DataConfig.CachePath, "Script.dll");


        if (!File.Exists(dll))
            throw new Exception($"Script compiler failed!");

        var bytes = File.ReadAllBytes(dll);
        return Assembly.Load(bytes);
    }

    private static Assembly CompileScripts()
    {
        try
        {
            if(File.Exists(Path.Combine(ServerConfig.DataConfig.CachePath, "Script.dll")))
                File.Delete(Path.Combine(ServerConfig.DataConfig.CachePath, "Script.dll"));

            var compiler = new ScriptCompiler();

            ServerLogger.Log("Compiling server side scripts...");

            var hasNewScripts = false;

            var path = Path.Combine(ServerConfig.DataConfig.DataPath, "Script/");
            foreach (var file in Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories))
            {
                ServerLogger.LogVerbose("Compiling script " + Path.GetRelativePath(path, file));
                hasNewScripts |= compiler.Compile(file);
            }

            ServerLogger.Log("Generating and loading script assembly...");

            var assembly = compiler.Load(!hasNewScripts);

            ServerLogger.Log("Server scripts loaded!");

            return assembly;
        }
        catch (Exception ex)
        {
            ServerLogger.LogError("Failed to compile scripts due to exception: " + ex.Message);
            throw;
        }
    }
 }