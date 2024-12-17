using System.Collections.ObjectModel;
using System.Reflection;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Config;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Logging;
using RoRebuildServer.ScriptSystem;
using RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;
using RoServerScript;

namespace RoRebuildServer.Data;

#pragma warning disable CS8618

public static class DataManager
{
    public static ReadOnlyDictionary<int, MonsterDatabaseInfo> MonsterIdLookup;
    public static ReadOnlyDictionary<string, MonsterDatabaseInfo> MonsterCodeLookup;
    public static ReadOnlyDictionary<string, MonsterDatabaseInfo> MonsterNameLookup;
    public static Dictionary<string, MonsterSkillAiBase> MonsterSkillAiHandlers;
    public static List<string> MvpMonsterCodes;
    public static List<InstanceEntry> InstanceList;
    public static ReadOnlyDictionary<string, Action<ServerMapConfig>> MapConfigs;

    public static Assembly ScriptAssembly;
    public static NpcBehaviorManager NpcManager;

    public static ReadOnlyDictionary<CharacterSkill, SkillData> SkillData;
    public static ReadOnlyDictionary<int, JobInfo> JobInfo;
    public static ReadOnlyDictionary<string, int> JobIdLookup;
    public static ReadOnlyDictionary<string, int> ItemIdByName;
    public static ReadOnlyDictionary<int, ItemInfo> ItemList;
    public static Dictionary<string, int> EffectIdForName;
    public static ReadOnlyDictionary<string, MonsterDropData> MonsterDropData;
    public static ReadOnlyDictionary<int, PlayerSkillTree> SkillTree; //SkillTree[Job][Skill] { (Prereq, lvl) } 
    public static ReadOnlyDictionary<int, int[]> JobMaxHpLookup;
    public static ReadOnlyDictionary<int, int[]> JobMaxSpLookup;
    public static ReadOnlyDictionary<string, SavePosition> SavePoints;
    public static ReadOnlyDictionary<int, int> JobExtendsList;

    public static ReadOnlyDictionary<string, int> WeaponClasses;
    public static ReadOnlyDictionary<string, HashSet<int>> EquipGroupInfo;
    public static ReadOnlyDictionary<int, WeaponInfo> WeaponInfo;
    public static ReadOnlyDictionary<int, ArmorInfo> ArmorInfo;
    public static ReadOnlyDictionary<int, CardInfo> CardInfo;
    public static ReadOnlyDictionary<int, AmmoInfo> AmmoInfo;
    public static ReadOnlyDictionary<int, UseItemInfo> UseItemInfo;
    public static ReadOnlyDictionary<string, List<string>> ItemMonsterSummonList;

    private static List<MonsterDatabaseInfo> monsterStats;
    private static List<List<MonsterAiEntry>> monsterAiList;
    private static List<MapEntry> mapList;
    private static int[] refineSuccessTable;

    public static List<MapEntry> Maps => mapList;
    
    public static ExpChart ExpChart;
    public static ElementChart ElementChart;
    
    public static bool IsJobInEquipGroup(string equipGroup, int job) => EquipGroupInfo.TryGetValue(equipGroup, out var set) && set.Contains(job);
    public static int GetEffectForItem(int item) => UseItemInfo.TryGetValue(item, out var effect) ? effect.Effect : -1;
    public static int GetWeightForItem(int item) => ItemList.TryGetValue(item, out var itemOut) ? itemOut.Weight : 0;
    public static ItemInfo? GetItemInfoById(int id) => ItemList.TryGetValue(id, out var item) ? item : null;
    public static int GetRefineSuccessForItem(int rank, int startingRefine) => refineSuccessTable[startingRefine * 5 + rank];

    public static List<MonsterAiEntry> GetAiStateMachine(MonsterAiType monsterType)
    {
        return monsterAiList[(int)monsterType];
    }

    public static int GetSpForSkill(CharacterSkill skill, int level)
    {
        if (!DataManager.SkillData.TryGetValue(skill, out var data))
            return 0;

        if (data.SpCost == null || data.SpCost.Length == 0)
            return 0;

        if (data.SpCost.Length < level)
            level = data.SpCost.Length;

        return data.SpCost[level-1];
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

    public static void RegisterMonsterSkillHandler(string name, MonsterSkillAiBase handler, bool isUnassignedAiType = false)
    {
        MonsterSkillAiHandlers.Add(name, handler);
        if (isUnassignedAiType)
            handler.IsUnassignedAiType = true;
    }

    public static void ReloadScripts()
    {
        var loader = new DataLoader();

        monsterStats = loader.LoadMonsterStats();
        monsterAiList = loader.LoadAiStateMachines();
        ExpChart = loader.LoadExpChart();
        EffectIdForName = loader.LoadEffectIds();
        JobInfo = loader.LoadJobs();
        JobIdLookup = loader.GetJobIdLookup(JobInfo);
        MvpMonsterCodes = loader.LoadMvpList();
        SkillData = loader.LoadSkillData();
        JobMaxHpLookup = loader.LoadMaxHpChart();
        JobMaxSpLookup = loader.LoadMaxSpChart();

        WeaponClasses = loader.LoadWeaponClasses();
        EquipGroupInfo = loader.LoadEquipGroups();
        var items = loader.LoadItemsRegular();
        ArmorInfo = loader.LoadItemsArmor(items);
        WeaponInfo = loader.LoadItemsWeapon(items);
        CardInfo = loader.LoadItemsCards(items);
        UseItemInfo = loader.LoadUseableItems(items); //dependent on effectId loaded earlier
        AmmoInfo = loader.LoadItemsAmmo(items);
        refineSuccessTable = loader.LoadRefineSuccessTable();

        ItemList = items.AsReadOnly();

        ItemIdByName = loader.GenerateItemIdByNameLookup();
        SavePoints = loader.LoadSavePoints().AsReadOnly();
        ElementChart = loader.LoadElementChart();
        MvpMonsterCodes = loader.LoadMvpList();
        
        //load our compiled script assemblies
        ScriptAssembly = ScriptLoader.LoadAssembly();
        NpcManager = new NpcBehaviorManager();
        
        var monsterIdLookup = new Dictionary<int, MonsterDatabaseInfo>(monsterStats.Count);
        var monsterCodeLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);
        var monsterNameLookup = new Dictionary<string, MonsterDatabaseInfo>(monsterStats.Count);

        foreach (var m in monsterStats)
        {
            monsterIdLookup.Add(m.Id, m);
            monsterCodeLookup.Add(m.Code, m);
            monsterNameLookup.TryAdd(m.Name, m);
        }

        MonsterIdLookup = monsterIdLookup.AsReadOnly();
        MonsterCodeLookup = monsterCodeLookup.AsReadOnly();
        MonsterNameLookup = monsterNameLookup.AsReadOnly();

        //things that require our compiled scripts
        loader.LoadMonsterSpawnMinions();
        loader.LoadNpcScripts(Assembly.GetAssembly(typeof(FirewallObjectEvent))!); //load from local assembly
        loader.LoadNpcScripts(ScriptAssembly);
        loader.LoadItemInteractions(ScriptAssembly);
        loader.LoadMonsterSkillAi(ScriptAssembly);
        
        //things that require other things loaded first
        MapConfigs = loader.LoadMapConfigs(ScriptAssembly);
        SkillTree = loader.LoadSkillTree();
        MonsterDropData = loader.LoadMonsterDropChanceData();
        ItemMonsterSummonList = loader.LoadMonsterSummonItemList();
    }

    public static void Initialize()
    {
        var loader = new DataLoader();

        mapList = loader.LoadMaps();
        InstanceList = loader.LoadInstances();

        ReloadScripts();

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