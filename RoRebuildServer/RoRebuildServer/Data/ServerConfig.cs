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


    public static ServerDataConfig ServerDataConfig
    {
        get
        {
            if (dataConfig != null) return dataConfig;

            configuration ??= GetConfig();
            dataConfig = configuration.GetSection("ServerDataConfig").Get<ServerDataConfig>();
            return dataConfig;
        }
    }
    
    private static ServerEntryConfig? entryConfig;
    private static ServerDataConfig? dataConfig;
    private static IConfiguration? configuration;

    public static IConfiguration GetConfig()
    {
        if(configuration != null)
            return configuration;

        configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets("RoRebuildServer-2d3ccb1b-373d-43ec-b059-5e7dc1bb4316")
            .AddEnvironmentVariables()
            .Build();
        return configuration;
    }
}