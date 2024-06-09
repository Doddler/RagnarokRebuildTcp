using System.Reflection;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Config;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Logging;
using RoRebuildServer.ScriptSystem;

namespace RoRebuildServer.Data;

#pragma warning disable CS8618

public static class DataManager
{
    private static List<MonsterDatabaseInfo> monsterStats;
    public static Dictionary<int, MonsterDatabaseInfo> MonsterIdLookup;
    public static Dictionary<string, MonsterDatabaseInfo> MonsterCodeLookup;
    public static Dictionary<string, MonsterDatabaseInfo> MonsterNameLookup;
    public static Dictionary<string, MonsterSkillAiBase> MonsterSkillAiHandlers;

    private static List<List<MonsterAiEntry>> monsterAiList;
    
    private static List<MapEntry> mapList;

    public static Assembly ScriptAssembly;
    public static NpcBehaviorManager NpcManager;
    
    public static List<InstanceEntry> InstanceList;
    public static Dictionary<string, Action<ServerMapConfig>> MapConfigs;

    public static Dictionary<int, JobInfo> JobInfo;
    public static Dictionary<string, int> JobIdLookup;
    public static Dictionary<string, int> ItemIdByName;
    public static Dictionary<int, ItemInfo> ItemList;
    public static List<string> MvpMonsterCodes;

    public static Dictionary<string, int> EffectIdForName;

    public static Dictionary<string, SavePosition> SavePoints;

    public static List<MapEntry> Maps => mapList;
    
    public static ExpChart ExpChart;
    public static ElementChart ElementChart;
    
    public static List<MonsterAiEntry> GetAiStateMachine(MonsterAiType monsterType)
    {
        return monsterAiList[(int)monsterType];
    }
    
    public static void RegisterItem(string name, ItemInteractionBase item)
    {
        if (!ItemIdByName.TryGetValue(name, out var id))
        {
            ServerLogger.LogWarning($"Could not attach item interaction to item as the item list does not contain: {name}");
            return;
        }

        ItemList[id].Interaction = item;
    }

    public static void RegisterNpc(string name, string map, string? signalName, string sprite, int x, int y, int facing, int w, int h, bool hasInteract, bool hasTouch, NpcBehaviorBase npcBehavior)
    {
        if (!MonsterCodeLookup.TryGetValue(sprite, out var md))
        {
            ServerLogger.LogError($"Could not load NPC '{name}' as the sprite {sprite} was not recognized by the server.");
            return;
        }

        NpcManager.RegisterNpc(name, map, signalName, md.Id, x, y, (Direction)facing, w, h, hasInteract, hasTouch, npcBehavior);
    }

    public static void RegisterEvent(string name, NpcBehaviorBase npcBehavior)
    {
        NpcManager.RegisterEvent(name, npcBehavior);
    }

    public static void RegisterMonsterSkillHandler(string name, MonsterSkillAiBase handler)
    {
        MonsterSkillAiHandlers.Add(name, handler);
    }

    public static void ReloadScripts()
    {
        var loader = new DataLoader();

        //only things that are safe to reload
        monsterStats = loader.LoadMonsterStats();
        monsterAiList = loader.LoadAiStateMachines();
        ExpChart = loader.LoadExpChart();
        EffectIdForName = loader.LoadEffectIds();
        SavePoints = loader.LoadSavePoints();
        MvpMonsterCodes = loader.LoadMvpList();

        //rebuild script assemblies
        ScriptAssembly = ScriptLoader.LoadAssembly();
        NpcManager = new NpcBehaviorManager();
        
        MonsterIdLookup = new Dictionary<int, MonsterDatabaseInfo>(monsterStats.Count);
        MonsterCodeLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);
        MonsterNameLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);

        foreach (var m in monsterStats)
        {
            MonsterIdLookup.Add(m.Id, m);
            MonsterCodeLookup.Add(m.Code, m);
            MonsterNameLookup.TryAdd(m.Name, m);
        }

        //the things we actually want to load
        loader.LoadNpcScripts(ScriptAssembly);
        MapConfigs = loader.LoadMapConfigs(ScriptAssembly);
        loader.LoadItemInteractions(ScriptAssembly);
        loader.LoadMonsterSkillAi(ScriptAssembly);
    }
    
    public static void Initialize()
    {
        //Config = ServerConfig.GetConfigSection<ServerDataConfig>();
        //ServerLogger.Log($"Loading server data at path: " + Config.DataPath);

        var loader = new DataLoader();

        ScriptAssembly = ScriptLoader.LoadAssembly();
        NpcManager = new NpcBehaviorManager();

        //configValues = loader.LoadServerConfig();
        mapList = loader.LoadMaps();
        InstanceList = loader.LoadInstances();
        //mapConnectorLookup = loader.LoadConnectors(mapList);
        monsterStats = loader.LoadMonsterStats();
        //mapSpawnInfo = loader.LoadSpawnInfo();
        monsterAiList = loader.LoadAiStateMachines();
        ExpChart = loader.LoadExpChart();
        EffectIdForName = loader.LoadEffectIds();
        JobInfo = loader.LoadJobs();
        JobIdLookup = loader.GetJobIdLookup(JobInfo);
        ItemList = loader.LoadItemList();
        ItemIdByName = loader.GenerateItemIdByNameLookup();
        SavePoints = loader.LoadSavePoints();
        ElementChart = loader.LoadElementChart();
        MvpMonsterCodes = loader.LoadMvpList();
        loader.LoadItemInteractions(ScriptAssembly);
        
        MonsterIdLookup = new Dictionary<int, MonsterDatabaseInfo>(monsterStats.Count);
        MonsterCodeLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);
        MonsterNameLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);

        foreach (var m in monsterStats)
        {
            MonsterIdLookup.Add(m.Id, m);
            MonsterCodeLookup.Add(m.Code, m);
            MonsterNameLookup.TryAdd(m.Name, m);
        }

        loader.LoadMonsterSpawnMinions();
        loader.LoadNpcScripts(ScriptAssembly);
        loader.LoadMonsterSkillAi(ScriptAssembly);
        MapConfigs = loader.LoadMapConfigs(ScriptAssembly);


        //special handling for if we start the map in single map only mode, removes all other maps from the server instance list
        var debug = ServerConfig.DebugConfig;
        if (debug.DebugMapOnly)
        {
            var okInstance = InstanceList.FirstOrDefault(i => i.Maps.Contains(debug.DebugMapName));
            if (okInstance == null)
            {
                ServerLogger.LogWarning($"Server started in Debug Map Only mode, but the specified map {debug.DebugMapName} was not found on the instance list.");
                return;
            }

            ServerLogger.Log($"Starting server in Debug Map Only mode. The map {debug.DebugMapName} is the only map available.");

            okInstance.Maps.RemoveAll(m => m != debug.DebugMapName);
            InstanceList.Clear();
            InstanceList.Add(okInstance);
        }
    }
}