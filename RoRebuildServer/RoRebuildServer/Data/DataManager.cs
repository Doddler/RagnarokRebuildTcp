using System.Reflection;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Config;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Data;

#pragma warning disable CS8618

public static class DataManager
{
    private static List<MonsterDatabaseInfo> monsterStats;
    public static Dictionary<int, MonsterDatabaseInfo> MonsterIdLookup;
    public static Dictionary<string, MonsterDatabaseInfo> MonsterCodeLookup;

    private static List<List<MonsterAiEntry>> monsterAiList;
    
    private static List<MapEntry> mapList;

    public static Assembly ScriptAssembly;
    public static NpcBehaviorManager NpcManager;
    
    public static List<InstanceEntry> InstanceList;
    public static Dictionary<string, Action<ServerMapConfig>> MapConfigs;

    public static Dictionary<string, int> ItemIdByName;
    public static Dictionary<int, ItemInfo> ItemList;

    public static Dictionary<string, int> EffectIdForName;

    public static List<MapEntry> Maps => mapList;
    

    public static ExpChart ExpChart;
    
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

    public static void RegisterNpc(string name, string map, string sprite, int x, int y, int facing, int w, int h, bool hasInteract, bool hasTouch, NpcBehaviorBase npcBehavior)
    {
        if (!MonsterCodeLookup.TryGetValue(sprite, out var md))
        {
            ServerLogger.LogError($"Could not load NPC '{name}' as the sprite {sprite} was not recognized by the server.");
            return;
        }

        NpcManager.RegisterNpc(name, map, md.Id, x, y, (Direction)facing, w, h, hasInteract, hasTouch, npcBehavior);
    }
    
    public static void Initialize()
    {
        //Config = ServerConfig.GetConfigSection<ServerDataConfig>();
        //ServerLogger.Log($"Loading server data at path: " + Config.DataPath);

        var loader = new DataLoader();

        ScriptAssembly = loader.CompileScripts();
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
        ItemList = loader.LoadItemList();
        ItemIdByName = loader.GenerateItemIdByNameLookup();
        loader.LoadItemInteractions(ScriptAssembly);
        
        MonsterIdLookup = new Dictionary<int, MonsterDatabaseInfo>(monsterStats.Count);
        MonsterCodeLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);

        foreach (var m in monsterStats)
        {
            MonsterIdLookup.Add(m.Id, m);
            MonsterCodeLookup.Add(m.Code, m);
        }

        loader.LoadNpcScripts(ScriptAssembly);
        MapConfigs = loader.LoadMapConfigs(ScriptAssembly);
    }
}