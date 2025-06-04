using Microsoft.Extensions.Configuration;
using RoRebuildServer.Data;

namespace RoWikiGenerator;

internal static class AppSettings
{
    public static string BasePath;
    public static string ServerPath;
    public static string ClientProjectPath;
    public static string TargetPath;

    public static void LoadConfigFromServerPath()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("RoWikiGeneratorSettings.json", optional: false, reloadOnChange: true);

        IConfigurationRoot configuration = builder.Build();

        ServerPath = configuration.GetValue<string>("ServerPath");
        ClientProjectPath = configuration.GetValue<string>("ClientProjectPath");
        TargetPath = configuration.GetValue<string>("TargetPath");

        if (ServerPath == null)
            throw new Exception($"You must specify a ConfigPath value in RoWikiGeneratorSettings.json!");

        BasePath = AppContext.BaseDirectory;
        if (BasePath.Contains("bin") && !File.Exists(Path.Combine(ServerPath, "appsettings.json")))
            BasePath = BasePath.Substring(0, AppContext.BaseDirectory.LastIndexOf("bin", StringComparison.Ordinal));

        ServerPath = Path.Combine(BasePath, ServerPath);
        ServerPath = Path.GetFullPath(ServerPath);
        ServerConfig.LoadConfigFromPath(ServerPath);

        ClientProjectPath = Path.Combine(BasePath, ClientProjectPath);
        ClientProjectPath = Path.GetFullPath(ClientProjectPath);


        TargetPath = Path.Combine(BasePath, TargetPath);
        TargetPath = Path.GetFullPath(TargetPath);

        var dataConfig = ServerConfig.DataConfig;
        dataConfig.CachePath = Path.GetFullPath(Path.Combine(ServerPath, dataConfig.CachePath));
        dataConfig.DataPath = Path.GetFullPath(Path.Combine(ServerPath, dataConfig.DataPath));
        dataConfig.WalkPathData = Path.GetFullPath(Path.Combine(ServerPath, dataConfig.WalkPathData));
    }
}