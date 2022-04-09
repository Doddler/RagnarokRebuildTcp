using RoRebuildServer.Data.Config;

namespace RoRebuildServer.Data;

public static class ServerConfig
{
    public const int MaxViewDistance = 21;
    public const int MaxAoESize = 9;
    public const int InitialConnectionCapacity = 40;

    public static ServerEntryConfig ServerEntryConfig
    {
        get
        {
            if (entryConfig != null) return entryConfig;

            configuration ??= GetConfig();
            entryConfig = configuration.GetSection("ServerCharacterEntry").Get<ServerEntryConfig>();
            return entryConfig;
        }
    }

    private static ServerEntryConfig? entryConfig;
    private static IConfiguration? configuration;

    public static IConfiguration GetConfig()
    {
        if(configuration != null)
            return configuration;

        configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets("RoRebuildServer")
            .AddEnvironmentVariables()
            .Build();
        return configuration;
    }
}