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
using RoWikiGenerator.Generators;
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

    public static async Task<string> RenderPage<TModel, TPage>(TModel model) where TPage : IComponent
    {
        var htmlRenderer = GetRenderer();

        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var dictionary = new Dictionary<string, object?>
            {
                { "Model", model }
            };

            var parameters = ParameterView.FromDictionary(dictionary);
            var output = await htmlRenderer.RenderComponentAsync<TPage>(parameters);
            return output.ToHtmlString();
        });

        return html;
    }

    public static async Task<string> InsertContentIntoTemplate(string content, string title, bool isSubDir = false)
    {
        var htmlRenderer = GetRenderer();

        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var dictionary = new Dictionary<string, object?>
            {
                { "Title", title },
                { "Content", content },
                { "IsSubDir", isSubDir }
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

    public static void CopyFilesIfNotExists(string srcPath, string destPath, string searchPattern)
    {
        if (!Directory.Exists(destPath))
            Directory.CreateDirectory(destPath);

        foreach (var icon in Directory.GetFiles(srcPath, "*.png"))
        {
            var fName = Path.GetFileName(icon);
            var destName = Path.Combine(destPath, fName);
            if (File.Exists(destName))
                continue;

            File.Copy(icon, destName);
        }
    }

    static async Task Main(string[] args)
    {
        //ServerLogger.Log("Ragnarok Rebuild Zone Server, starting up!");
        AppSettings.LoadConfigFromServerPath();
        DataManager.Initialize(true);
        WikiData.LoadData();


        WikiData.World = new World();


        //FixName();
        //DistanceCache.Init();
        //RoDatabase.Initialize();

        //var cfg = ServerConfig.GetConfig();

        CopyFilesIfNotExists(Path.Combine(AppSettings.ClientProjectPath, "Assets\\Sprites\\Imported\\Icons\\Sprites"),
            Path.Combine(AppSettings.TargetPath, "images\\rebuilditems"), "*.png");

        CopyFilesIfNotExists(Path.Combine(AppSettings.ClientProjectPath, "Assets\\Sprites\\Imported\\Collections"),
            Path.Combine(AppSettings.TargetPath, "images\\collections"), "*.png");

        ServerLogger.Log($"Min spawn time: {ServerConfig.DebugConfig.MinSpawnTime}");
        ServerLogger.Log($"Max spawn time: {ServerConfig.DebugConfig.MaxSpawnTime}");

        if (!Directory.Exists(Path.Combine(AppSettings.TargetPath, "jobs/")))
            Directory.CreateDirectory(Path.Combine(AppSettings.TargetPath, "jobs/"));
        if (!Directory.Exists(Path.Combine(AppSettings.TargetPath, "maps/")))
            Directory.CreateDirectory(Path.Combine(AppSettings.TargetPath, "maps/"));

        SetUpServiceProvider();
        Items.PrepareSharedItemIcons();
        Maps.PrepareDungeonGroupings();
        Maps.PrepareFieldGroupings();

        //monsters
        var content = await InsertContentIntoTemplate(await Monsters.RenderMonsterPage(), "Ragnarok Renewal Monsters");
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "MonsterListFull.html"), content);

        //jobs
        content = await InsertContentIntoTemplate(await Jobs.GetJobPageData(0), "Ragnarok Renewal - Novice", true);
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "jobs/JobNovice.html"), content);
        content = await InsertContentIntoTemplate(await Jobs.GetJobPageData(1), "Ragnarok Renewal - Swordsman", true);
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "jobs/JobSwordsman.html"), content);
        content = await InsertContentIntoTemplate(await Jobs.GetJobPageData(3), "Ragnarok Renewal - Mage", true);
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "jobs/JobMage.html"), content);
        content = await InsertContentIntoTemplate(await Jobs.GetJobPageData(2), "Ragnarok Renewal - Archer", true);
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "jobs/JobArcher.html"), content);
        content = await InsertContentIntoTemplate(await Jobs.GetJobPageData(5), "Ragnarok Renewal - Thief", true);
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "jobs/JobThief.html"), content);
        content = await InsertContentIntoTemplate(await Jobs.GetJobPageData(4), "Ragnarok Renewal - Acolyte", true);
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "jobs/JobAcolyte.html"), content);
        content = await InsertContentIntoTemplate(await Jobs.GetJobPageData(6), "Ragnarok Renewal - Merchant", true);
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "jobs/JobMerchant.html"), content);

        //items
        Items.LoadItemSourceFromNpcs();
        Items.PrepareItems();

        content = await InsertContentIntoTemplate(await Items.GetCardPage(), "Ragnarok Renewal : Items - Cards");
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "rebuildCards.html"), content);

        content = await InsertContentIntoTemplate(await Items.GetWeaponPage(), "Ragnarok Renewal : Items - Weapons");
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "rebuildWeapons.html"), content);

        content = await InsertContentIntoTemplate(await Items.GetEquipmentPage(), "Ragnarok Renewal : Items - Equipment");
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "rebuildEquipment.html"), content);


        content = await InsertContentIntoTemplate(await Maps.GetWorldMap(), "Ragnarok Renewal : World Map");
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, "WorldMap.html"), content);

        foreach (var map in DataManager.Maps)
        {
            content = await InsertContentIntoTemplate(await Maps.GetSpecificMap(map.Code), $"Ragnarok Renewal : {map.Name} ({map.Code})", true);
            await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, $"maps/{map.Code}.html"), content);
        }
        
        content = await InsertContentIntoTemplate(await Maps.GetDungeons(), $"Ragnarok Renewal : Dungeon Maps", false);
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, $"Dungeons.html"), content);

        content = await InsertContentIntoTemplate(await Maps.GetFields(), $"Ragnarok Renewal : Field Maps", false);
        await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, $"FieldMaps.html"), content);


        foreach (var (region, maps) in Maps.DungeonGrouping)
        {
            content = await InsertContentIntoTemplate(await Maps.RenderRegion(region, maps), $"Ragnarok Renewal : {region}", true);
            await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, $"regions/{region.Replace(" ", "_")}.html"), content);
        }

        foreach (var (region, maps) in Maps.FieldGrouping)
        {
            content = await InsertContentIntoTemplate(await Maps.RenderRegion(region, maps), $"Ragnarok Renewal : {region}", true);
            await File.WriteAllTextAsync(Path.Combine(AppSettings.TargetPath, $"regions/{region.Replace(" ", "_")}.html"), content);
        }


        //var world = new World();
        //NetworkManager.Init(world);

        ServerLogger.Log("File generation complete!");
    }
}