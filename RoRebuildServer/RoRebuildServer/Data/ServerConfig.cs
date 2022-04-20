using RoRebuildServer.Data.Config;

namespace RoRebuildServer.Data;

public static class ServerConfig
{
    public const int MaxViewDistance = 21;
    public const int MaxAoESize = 9;
    public const int InitialConnectionCapacity = 40;

    public static T GetConfigSection<T>()
    {
        configuration ??= GetConfig();
        return configuration.GetSection(typeof(T).Name).Get<T>();
    }
    
    private static IConfiguration? configuration;

    public static IConfiguration GetConfig()
    {
        if(configuration != null)
            return configuration;

        configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
            .AddUserSecrets("RoRebuildServer-2d3ccb1b-373d-43ec-b059-5e7dc1bb4316")
            .AddEnvironmentVariables()
            .Build();
        return configuration;
    }
}