using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Database;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;
using RoWikiGenerator.Pages;

namespace RoWikiGenerator;

public class Program
{
    //static void FixName()
    //{
    //    var lines = File.ReadAllLines(@"G:\Projects2\Ragnarok\Zone\npcdata\mobname.def");
    //    foreach (var line in lines)
    //    {
    //        var s = line.Split(' ');
    //        if (s.Length < 2)
    //            continue;

    //        var id = int.Parse(s[1]);
    //        var txt = s[0].ToLower();
    //        var path = $"../../../images/monsters/{id}.png";
    //        if (!File.Exists(path))
    //            continue;

    //        File.Copy(path, $"../../../images/rebuildmonsters/{txt}.png");

    //        Console.WriteLine(path);

    //    }
    //}

    public static async Task<string> RenderMonsters(MonsterModel model)
    {
        var htmlRenderer = GetRenderer();

        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var dictionary = new Dictionary<string, object?>
            {
                { "Model", model }
            };

            var parameters = ParameterView.FromDictionary(dictionary);
            var output = await htmlRenderer.RenderComponentAsync<RebuildMonsters>(parameters);
            return output.ToHtmlString();
        });

        return html;
    }

    public static async Task<string> InsertContentIntoTemplate(string content, string title)
    {
        var htmlRenderer = GetRenderer();

        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var dictionary = new Dictionary<string, object?>
            {
                { "Title", title },
                { "Content", content },
            };

            var parameters = ParameterView.FromDictionary(dictionary);
            var output = await htmlRenderer.RenderComponentAsync<PageTemplate>(parameters);
            return output.ToHtmlString();
        });

        return html;
    }

    private static IServiceCollection services;
    private static IServiceProvider serviceProvider;
    private static ILoggerFactory loggerFactory;

    public static HtmlRenderer GetRenderer()
    {
        return new HtmlRenderer(serviceProvider, loggerFactory);
    }

    public static void SetUpServiceProvider()
    {
        services = new ServiceCollection();
        services.AddLogging();

        serviceProvider = services.BuildServiceProvider();
        loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    }

    public class MonsterModel
    {
        public Dictionary<int, List<(string map, MapSpawnRule spawn)>> monsterMapSpawns;
        public Dictionary<string, string> sharedIcons;
        public List<MonsterDatabaseInfo> monsters;
    }

    static async Task Main(string[] args)
    {
        ServerLogger.Log("Ragnarok Rebuild Zone Server, starting up!");

        //FixName();
        //DistanceCache.Init();
        //RoDatabase.Initialize();

        var sharedIcons = new Dictionary<string, string>();

        foreach (var l in File.ReadAllLines(
                     @"../../../../../RebuildClient/Assets/Data/SharedItemIcons.txt"))
        {
            var s = l.Split('\t');
            sharedIcons.Add(s[0], s[1]);
        }

        //var cfg = ServerConfig.GetConfig();

        Console.WriteLine($"Min spawn time: {ServerConfig.DebugConfig.MinSpawnTime}");
        Console.WriteLine($"Max spawn time: {ServerConfig.DebugConfig.MaxSpawnTime}");

        DataManager.Initialize(true);

        SetUpServiceProvider();


        var monsterMapSpawns = new Dictionary<int, List<(string map, MapSpawnRule spawn)>>();

        foreach (var map in DataManager.Maps)
        {
            var spawns = new WikiSpawnConfig();
            if (!DataManager.MapConfigs.TryGetValue(map.Code, out var loader))
                continue;
            if (!DataManager.InstanceList.Any(i => i.Maps.Contains(map.Code)))
                continue;

            loader(spawns);
            foreach (var spawn in spawns.SpawnRules)
            {
                if (!monsterMapSpawns.TryGetValue(spawn.MonsterDatabaseInfo.Id, out var monList))
                {
                    monList = new List<(string map, MapSpawnRule spawn)>();
                    monsterMapSpawns.Add(spawn.MonsterDatabaseInfo.Id, monList);
                }

                var existing = monList.FirstOrDefault(m =>
                    m.spawn.MonsterDatabaseInfo.Id == spawn.MonsterDatabaseInfo.Id && m.map == map.Code
                    && m.spawn.MinSpawnTime == spawn.MinSpawnTime && m.spawn.MaxSpawnTime == spawn.MaxSpawnTime);

                if (existing.spawn != null)
                    existing.spawn.Count += spawn.Count;
                else
                    monList.Add((map.Code, spawn));
            }
        }

        var monsters = DataManager.MonsterIdLookup.Select(m => m.Value)
            //.OrderBy(m => m.Level).ThenBy(m => m.Name).ToList();
            .OrderBy(m => m.Id).ToList();

        var model = new MonsterModel()
        {
            monsterMapSpawns = monsterMapSpawns,
            sharedIcons = sharedIcons,
            monsters = monsters
        };

        var monstersText = await RenderMonsters(model);
        var content = await InsertContentIntoTemplate(monstersText, "Ragnarok Renewal Monsters");

        await File.WriteAllTextAsync(@"../../../rebuildmonsters.html", content);

        //var world = new World();
        //NetworkManager.Init(world);

        Console.WriteLine("Hello, World!");
    }
}