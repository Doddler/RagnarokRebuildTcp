using System.IO;
using RoRebuildServer.Data.Config;

namespace RoRebuildServer.Data;

public static class ServerConfig
{
    public const int MaxViewDistance = 21;
    public const int MaxAoESize = 9;
    public const int InitialConnectionCapacity = 40;
    public const int MaxAttackRangeWhileBlind = 5;
    public const float ArrowTravelTime = 44f;

    private static ServerDebugConfig? serverDebugConfig;
    private static ServerDataConfig? serverDataConfig;
    private static ServerEntryConfig? serverEntryConfig;
    private static ServerOperationConfig? serverOperationConfig;

    public static ServerDebugConfig DebugConfig => serverDebugConfig ??= GetConfigSection<ServerDebugConfig>();
    public static ServerDataConfig DataConfig => serverDataConfig ??= GetConfigSection<ServerDataConfig>();
    public static ServerEntryConfig EntryConfig => serverEntryConfig ??= GetConfigSection<ServerEntryConfig>("ServerCharacterEntry");
    public static ServerOperationConfig OperationConfig => serverOperationConfig ??= GetConfigSection<ServerOperationConfig>();

    public static T GetConfigSection<T>()
    {
        configuration ??= GetConfig();
        var result = configuration.GetSection(typeof(T).Name).Get<T>()!;
        if (result is ServerDataConfig dataConfig)
        {
            dataConfig.DataPath = ResolveConfiguredPath(dataConfig.DataPath);
            dataConfig.WalkPathData = ResolveConfiguredPath(dataConfig.WalkPathData);
        }

        return result;
    }


    public static T GetConfigSection<T>(string sectionName)
    {
        configuration ??= GetConfig();
        return configuration.GetSection(sectionName).Get<T>()!;
    }

    private static string ResolveConfiguredPath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        // Configuration files are copied beside the compiled server, while
        // development data lives beside the server project. Try the launch
        // directory and each parent of the compiled output so relative paths
        // work from dotnet run, an IDE, and a published deployment alike.
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName,
            Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.FullName,
            Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.FullName,
            Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName,
        };

        foreach (var basePath in candidates)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                continue;

            var candidate = Path.GetFullPath(Path.Combine(basePath, path));
            if (Directory.Exists(candidate))
                return candidate;
        }

        // Preserve the configured value semantics, but make the eventual
        // error report show the exact path the server attempted.
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
    }

    public static T GetConfigValue<T>(string name)
    {
        configuration ??= GetConfig();
        return configuration.GetValue<T>(name)!;
    }

    private static IConfiguration? configuration;

    public static IConfiguration Configuration => configuration ??= GetConfig();

    public static IConfiguration GetConfig()
    {
        if (configuration != null)
            return configuration;

        configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Logging.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
            .AddUserSecrets("RoRebuildServer-2d3ccb1b-373d-43ec-b059-5e7dc1bb4316")
            .AddEnvironmentVariables()
            .Build();
        return configuration;
    }

    public static void LoadConfigFromPath(string path)
    {
        ArgumentNullException.ThrowIfNull("Configuration already loaded!");

        configuration = new ConfigurationBuilder()
            .SetBasePath(path)
            .AddJsonFile(Path.Combine(path, "appsettings.json"), optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(path, "appsettings.Logging.json"), optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(path, $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json"), optional: true, reloadOnChange: true)
            .AddUserSecrets("RoRebuildServer-2d3ccb1b-373d-43ec-b059-5e7dc1bb4316")
            .AddEnvironmentVariables()
            .Build();
    }
}
