using System.Reflection;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
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

    private static Dictionary<string, string> configValues;


    private static List<MapEntry> mapList;

    public static Assembly ScriptAssembly;
    public static NpcBehaviorManager NpcManager;
    
    public static List<InstanceEntry> InstanceList;
    public static Dictionary<string, Action<ServerMapConfig>> MapConfigs;

    public static List<MapEntry> Maps => mapList;
    

    public static ExpChart ExpChart;


    public static List<MonsterAiEntry> GetAiStateMachine(MonsterAiType monsterType)
    {
        return monsterAiList[(int)monsterType];
    }


    public static bool TryGetConfigValue(string key, out string? value)
    {
        if (configValues.TryGetValue(key, out value))
            return true;

        value = null;
        return false;
    }

    public static bool TryGetConfigInt(string key, out int value)
    {
        if (configValues.TryGetValue(key, out var val))
        {
            if (int.TryParse(val, out value))
                return true;
        }

        value = 0;
        return false;
    }

    public static void RegisterNpc(string name, string map, string sprite, int x, int y, int facing, int w, int h, bool hasInteract, bool hasTouch, NpcBehaviorBase npcBehavior)
    {
        if (!MonsterCodeLookup.TryGetValue(sprite, out var md))
        {
            ServerLogger.LogError($"Could not load NPC '{name}' as the sprite {sprite} was not recognized by teh server.");
            return;
        }

        NpcManager.RegisterNpc(name, map, md.Id, x, y, (Direction)facing, w, h, hasInteract, hasTouch, npcBehavior);
    }
    
    public static void Initialize()
    {
        var loader = new DataLoader();

        ScriptAssembly = loader.CompileScripts();
        NpcManager = new NpcBehaviorManager();

        configValues = loader.LoadServerConfig();
        mapList = loader.LoadMaps();
        InstanceList = loader.LoadInstances();
        //mapConnectorLookup = loader.LoadConnectors(mapList);
        monsterStats = loader.LoadMonsterStats();
        //mapSpawnInfo = loader.LoadSpawnInfo();
        monsterAiList = loader.LoadAiStateMachines();
        ExpChart = loader.LoadExpChart();
        
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