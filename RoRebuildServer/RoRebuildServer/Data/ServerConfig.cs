using RoRebuildServer.Data.Config;

namespace RoRebuildServer.Data;

public static class ServerConfig
{
    public const int MaxViewDistance = 21;
    public const int MaxAoESize = 9;
    public const int InitialConnectionCapacity = 40;

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
        return configuration.GetSection(typeof(T).Name).Get<T>();
    }


    public static T GetConfigSection<T>(string sectionName)
    {
        configuration ??= GetConfig();
        return configuration.GetSection(sectionName).Get<T>();
    }

    public static T GetConfigValue<T>(string name)
    {
        configuration ??= GetConfig();
        return configuration.GetValue<T>(name);
    }

    private static IConfiguration? configuration;

    public static IConfiguration Configuration => configuration ??= GetConfig();

    public static IConfiguration GetConfig()
    {
        if(configuration != null)
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