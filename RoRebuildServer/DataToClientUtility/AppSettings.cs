using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RoRebuildServer.Data;

namespace DataToClientUtility;

public class DataToClientUtilityConfig
{
    public string ConfigPath { get; set; }
}

internal static class AppSettings
{
    public static void LoadConfigFromServerPath()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("DataToClientUtilitySettings.json", optional: false, reloadOnChange: true);

        IConfigurationRoot configuration = builder.Build();

        var configPath = configuration.GetValue<string>("ConfigPath");

        if (configPath == null)
            throw new Exception($"You must specify a ConfigPath value in DataToClientUtilitySettings.json!");

        var basePath = AppContext.BaseDirectory;
        if (basePath.Contains("bin") && !File.Exists(Path.Combine(configPath, "appsettings.json")))
            basePath = basePath.Substring(0, AppContext.BaseDirectory.LastIndexOf("bin", StringComparison.Ordinal));

        configPath = Path.Combine(basePath, configPath);
        configPath = Path.GetFullPath(configPath);
        ServerConfig.LoadConfigFromPath(configPath);

        var dataConfig = ServerConfig.DataConfig;
        dataConfig.DataPath = Path.GetFullPath(Path.Combine(basePath, dataConfig.DataPath));
        dataConfig.WalkPathData = Path.GetFullPath(Path.Combine(basePath, dataConfig.WalkPathData));
    }
}